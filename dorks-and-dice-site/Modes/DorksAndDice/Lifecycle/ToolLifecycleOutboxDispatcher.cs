using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using dorks_and_dice_site.Services.Tools;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Modes.DorksAndDice.Lifecycle;

public interface IToolLifecycleOutboxDispatcher
{
    Task<int> DispatchDueAsync(CancellationToken cancellationToken = default);
}

public sealed class ToolLifecycleOutboxDispatcher(
    DorksAndDiceDbContext dbContext,
    IToolRegistry toolRegistry,
    IToolUpstreamPolicy upstreamPolicy,
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider,
    ILogger<ToolLifecycleOutboxDispatcher> logger) : IToolLifecycleOutboxDispatcher
{
    private const int BatchSize = 20;
    private const int MaxErrorLength = 2000;
    private const int ContractVersion = 1;

    public async Task<int> DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        // SQLite does not translate DateTimeOffset ordering/comparison. Keep the durable shape
        // provider-neutral by loading only undelivered rows from the database, then apply the
        // due-time comparison and bounded batch selection in memory. PostgreSQL uses the same
        // code path so retry semantics stay identical across supported providers.
        var undelivered = await dbContext.ToolLifecycleOutboxEvents
            .Where(item => item.DeliveredAt == null)
            .ToListAsync(cancellationToken);
        var pending = undelivered
            .Where(item => item.NextAttemptAt <= now)
            .OrderBy(item => item.NextAttemptAt)
            .ThenBy(item => item.OccurredAt)
            .Take(BatchSize)
            .ToList();

        foreach (var lifecycleEvent in pending)
        {
            lifecycleEvent.AttemptCount++;
            var error = await TryDeliverAsync(lifecycleEvent, cancellationToken);
            var completedAt = timeProvider.GetUtcNow();

            if (error is null)
            {
                lifecycleEvent.DeliveredAt = completedAt;
                lifecycleEvent.LastError = null;
            }
            else
            {
                lifecycleEvent.LastError = Truncate(error);
                lifecycleEvent.NextAttemptAt = completedAt.Add(RetryDelay(lifecycleEvent.AttemptCount));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return pending.Count;
    }

    private async Task<string?> TryDeliverAsync(
        ToolLifecycleOutboxEvent lifecycleEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            var tool = await toolRegistry.GetBySlugAsync(lifecycleEvent.TargetToolSlug, cancellationToken);
            if (tool is null)
            {
                return $"Tool '{lifecycleEvent.TargetToolSlug}' is not registered.";
            }

            if (!tool.Enabled)
            {
                return $"Tool '{lifecycleEvent.TargetToolSlug}' is disabled.";
            }

            if (!upstreamPolicy.TryBuild(
                    tool,
                    "/api/lifecycle/events",
                    QueryString.Empty,
                    out var deliveryUri,
                    out var validationError)
                || deliveryUri is null)
            {
                return validationError ?? $"Tool '{lifecycleEvent.TargetToolSlug}' has no valid lifecycle upstream.";
            }

            var context = new ToolLifecycleContext(
                ContractVersion,
                tool.Slug,
                lifecycleEvent.EventId,
                lifecycleEvent.EventType,
                lifecycleEvent.SubjectId,
                lifecycleEvent.OccurredAt);
            var ticket = ToolLifecycleTickets.Issue(context);
            var introspectionPath = $"/tool-host/{tool.Slug}/api/lifecycle/introspect";

            using var request = new HttpRequestMessage(HttpMethod.Post, deliveryUri);
            request.Headers.TryAddWithoutValidation(ToolLifecycleHeaders.Ticket, ticket);
            request.Headers.TryAddWithoutValidation(ToolLifecycleHeaders.IntrospectionPath, introspectionPath);

            using var response = await httpClientFactory
                .CreateClient(ToolHttpClientNames.Hosting)
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            return response.IsSuccessStatusCode
                ? null
                : $"Lifecycle delivery returned HTTP {(int)response.StatusCode}.";
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return "Lifecycle delivery timed out.";
        }
        catch (HttpRequestException exception)
        {
            return exception.Message;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Lifecycle delivery for event {EventId} failed before acknowledgment.",
                lifecycleEvent.EventId);
            return exception.Message;
        }
    }

    private static TimeSpan RetryDelay(int attemptCount)
    {
        var exponent = Math.Clamp(attemptCount - 1, 0, 9);
        return TimeSpan.FromSeconds(Math.Min(3600, 5 * Math.Pow(2, exponent)));
    }

    private static string Truncate(string error) =>
        error.Length <= MaxErrorLength ? error : error[..MaxErrorLength];
}

public sealed class ToolLifecycleDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ToolLifecycleDeliveryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IToolLifecycleOutboxDispatcher>();
                await dispatcher.DispatchDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Tool lifecycle outbox dispatch failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
