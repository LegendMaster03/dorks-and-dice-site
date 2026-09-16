using System.Net;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Modes.DorksAndDice.Lifecycle;
using dorks_and_dice_site.Modes.DorksAndDice.Persistence;
using dorks_and_dice_site.Services.Tools;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace dorks_and_dice_site.Tests;

public sealed class ToolLifecycleDeliveryTests
{
    [Fact]
    public async Task SuccessfulAcknowledgmentMarksOutboxDeliveredAndCarriesTrustedContext()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();

        var lifecycleEvent = PendingEvent();
        db.ToolLifecycleOutboxEvents.Add(lifecycleEvent);
        await db.SaveChangesAsync();

        HttpRequestMessage? captured = null;
        var handler = new RecordingHandler(request =>
        {
            captured = CloneRequest(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        var dispatcher = CreateDispatcher(db, handler);

        Assert.Equal(1, await dispatcher.DispatchDueAsync());

        Assert.NotNull(lifecycleEvent.DeliveredAt);
        Assert.Null(lifecycleEvent.LastError);
        Assert.Equal(1, lifecycleEvent.AttemptCount);
        Assert.NotNull(captured);
        Assert.Equal(new Uri("http://character-sheet:8080/api/lifecycle/events"), captured!.RequestUri);
        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.Equal(
            "/tool-host/character-sheet/api/lifecycle/introspect",
            captured.Headers.GetValues(ToolLifecycleHeaders.IntrospectionPath).Single());

        var ticket = captured.Headers.GetValues(ToolLifecycleHeaders.Ticket).Single();
        Assert.True(ToolLifecycleTickets.TryRedeem("character-sheet", ticket, out var context));
        Assert.NotNull(context);
        Assert.Equal(1, context.ContractVersion);
        Assert.Equal(lifecycleEvent.EventId, context.EventId);
        Assert.Equal(lifecycleEvent.EventType, context.EventType);
        Assert.Equal(lifecycleEvent.SubjectId, context.SubjectId);
        Assert.Equal(lifecycleEvent.OccurredAt, context.OccurredAt);
    }

    [Fact]
    public async Task FailedDeliveryStaysPendingAndCanBeRetriedSuccessfully()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();

        var lifecycleEvent = PendingEvent();
        db.ToolLifecycleOutboxEvents.Add(lifecycleEvent);
        await db.SaveChangesAsync();

        var attempts = 0;
        var handler = new RecordingHandler(_ =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(
                attempts == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
        });
        var dispatcher = CreateDispatcher(db, handler);

        Assert.Equal(1, await dispatcher.DispatchDueAsync());
        Assert.Null(lifecycleEvent.DeliveredAt);
        Assert.Equal(1, lifecycleEvent.AttemptCount);
        Assert.NotNull(lifecycleEvent.LastError);
        Assert.True(lifecycleEvent.NextAttemptAt > DateTimeOffset.UtcNow);

        lifecycleEvent.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();

        Assert.Equal(1, await dispatcher.DispatchDueAsync());
        Assert.NotNull(lifecycleEvent.DeliveredAt);
        Assert.Equal(2, lifecycleEvent.AttemptCount);
        Assert.Null(lifecycleEvent.LastError);
        Assert.Equal(2, attempts);
    }

    private static DorksAndDiceDbContext CreateDb(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<DorksAndDiceDbContext>()
            .UseSqlite(connection)
            .Options);

    private static ToolLifecycleOutboxEvent PendingEvent()
    {
        var now = DateTimeOffset.UtcNow.AddSeconds(-1);
        return new ToolLifecycleOutboxEvent
        {
            EventId = Guid.NewGuid(),
            TargetToolSlug = ToolLifecycleTargets.CharacterSheet,
            EventType = ToolLifecycleEventTypes.CharacterDeleted,
            SubjectId = Guid.NewGuid(),
            OccurredAt = now,
            NextAttemptAt = now
        };
    }

    private static ToolLifecycleOutboxDispatcher CreateDispatcher(
        DorksAndDiceDbContext db,
        HttpMessageHandler handler)
    {
        var tool = new ToolRegistration
        {
            Id = Guid.NewGuid(),
            Slug = ToolLifecycleTargets.CharacterSheet,
            DisplayName = "Character Sheet",
            UpstreamBaseUrl = "http://character-sheet:8080",
            Enabled = true
        };
        var registry = new FixedRegistry(tool);
        var factory = new FixedHttpClientFactory(new HttpClient(handler));
        var policy = new ToolUpstreamPolicy(new ConfigurationBuilder().Build());
        return new ToolLifecycleOutboxDispatcher(
            db,
            registry,
            policy,
            factory,
            TimeProvider.System,
            NullLogger<ToolLifecycleOutboxDispatcher>.Instance);
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        return clone;
    }

    private sealed class FixedRegistry(ToolRegistration tool) : IToolRegistry
    {
        public Task<IReadOnlyList<ToolRegistration>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ToolRegistration>>([tool]);

        public Task<ToolRegistration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<ToolRegistration?>(tool.Id == id ? tool : null);

        public Task<ToolRegistration?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
            Task.FromResult<ToolRegistration?>(
                string.Equals(tool.Slug, slug, StringComparison.OrdinalIgnoreCase) ? tool : null);

        public Task SaveAsync(ToolRegistration registration, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responder(request);
    }
}
