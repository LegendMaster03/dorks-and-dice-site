using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace dorks_and_dice_site.Services.Tools;

public static class ToolLifecycleHeaders
{
    public const string ReservedPrefix = "X-Dorks-Tool-Lifecycle-";
    public const string Ticket = "X-Dorks-Tool-Lifecycle-Ticket";
    public const string IntrospectionPath = "X-Dorks-Tool-Lifecycle-Introspection-Path";
}

public sealed record ToolLifecycleContext(
    int ContractVersion,
    string ToolSlug,
    Guid EventId,
    string EventType,
    Guid SubjectId,
    DateTimeOffset OccurredAt);

/// <summary>
/// Process-local, one-time capability tickets for Site-to-Tool lifecycle delivery. These tickets
/// are intentionally independent from user authentication tickets and never enter the browser
/// trust boundary.
/// </summary>
public static class ToolLifecycleTickets
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);
    private static readonly ConcurrentDictionary<string, TicketEntry> Tickets =
        new(StringComparer.Ordinal);

    public static string Issue(ToolLifecycleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var now = DateTimeOffset.UtcNow;
        RemoveExpired(now);

        while (true)
        {
            var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            if (Tickets.TryAdd(ticket, new TicketEntry(context, now.Add(Lifetime))))
            {
                return ticket;
            }
        }
    }

    public static bool TryRedeem(
        string toolSlug,
        string ticket,
        out ToolLifecycleContext? context)
    {
        context = null;
        if (string.IsNullOrWhiteSpace(toolSlug) || string.IsNullOrWhiteSpace(ticket))
        {
            return false;
        }

        if (!Tickets.TryRemove(ticket, out var entry)
            || entry.ExpiresAt <= DateTimeOffset.UtcNow
            || !string.Equals(entry.Context.ToolSlug, toolSlug, StringComparison.Ordinal))
        {
            return false;
        }

        context = entry.Context;
        return true;
    }

    private static void RemoveExpired(DateTimeOffset now)
    {
        foreach (var entry in Tickets)
        {
            if (entry.Value.ExpiresAt <= now)
            {
                Tickets.TryRemove(entry.Key, out _);
            }
        }
    }

    private sealed record TicketEntry(ToolLifecycleContext Context, DateTimeOffset ExpiresAt);
}
