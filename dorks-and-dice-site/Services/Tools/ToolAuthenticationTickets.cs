using System.Collections.Concurrent;
using System.Security.Cryptography;
using dorks_and_dice_site.Models.Tools;

namespace dorks_and_dice_site.Services.Tools;

public static class ToolAuthenticationHeaders
{
    public const string ReservedPrefix = "X-Dorks-Tool-Auth-";
    public const string Ticket = "X-Dorks-Tool-Auth-Ticket";
    public const string IntrospectionPath = "X-Dorks-Tool-Auth-Introspection-Path";
}

/// <summary>
/// Process-local, one-time authentication tickets used only for the short server-to-server hop
/// from the site gateway to a Tool. The browser never receives the ticket. A future multi-instance
/// site deployment should replace this store with shared ephemeral storage without changing the
/// Tool contract.
/// </summary>
public static class ToolAuthenticationTickets
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);
    private static readonly ConcurrentDictionary<string, TicketEntry> Tickets =
        new(StringComparer.Ordinal);

    public static string Issue(ToolHostAuthenticationContext context)
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
        out ToolHostAuthenticationContext? context)
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

    private sealed record TicketEntry(
        ToolHostAuthenticationContext Context,
        DateTimeOffset ExpiresAt);
}
