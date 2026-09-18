using System.Collections.Concurrent;
using System.Security.Cryptography;
using dorks_and_dice_site.Models.Tools;

namespace dorks_and_dice_site.Services.Tools;

public static class ToolDelegationHeaders
{
    public const string ReservedPrefix = "X-Dorks-Tool-Delegation-";
    public const string Capability = "X-Dorks-Tool-Delegation-Capability";
    public const string Path = "X-Dorks-Tool-Delegation-Path";
}

public interface IToolDelegationCapabilityService
{
    string Issue(string sourceToolSlug, ToolHostAuthenticationContext authenticationContext);

    bool TryUse(
        string sourceToolSlug,
        string capability,
        out ToolHostAuthenticationContext? authenticationContext);
}

public sealed class ToolDelegationCapabilityService : IToolDelegationCapabilityService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);
    public const int MaximumUses = 32;

    private readonly ConcurrentDictionary<string, CapabilityEntry> _capabilities =
        new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public ToolDelegationCapabilityService()
        : this(TimeProvider.System)
    {
    }

    public ToolDelegationCapabilityService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public string Issue(
        string sourceToolSlug,
        ToolHostAuthenticationContext authenticationContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceToolSlug);
        ArgumentNullException.ThrowIfNull(authenticationContext);

        if (!string.Equals(
                sourceToolSlug,
                authenticationContext.ToolSlug,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The source Tool slug must match the authoritative authentication context.",
                nameof(sourceToolSlug));
        }

        var now = _timeProvider.GetUtcNow();
        RemoveExpired(now);

        while (true)
        {
            var capability = $"ddtd_v1_{Convert.ToHexString(RandomNumberGenerator.GetBytes(32))}";
            if (_capabilities.TryAdd(
                    capability,
                    new CapabilityEntry(
                        sourceToolSlug,
                        authenticationContext,
                        now.Add(Lifetime))))
            {
                return capability;
            }
        }
    }

    public bool TryUse(
        string sourceToolSlug,
        string capability,
        out ToolHostAuthenticationContext? authenticationContext)
    {
        authenticationContext = null;
        if (string.IsNullOrWhiteSpace(sourceToolSlug)
            || string.IsNullOrWhiteSpace(capability)
            || !_capabilities.TryGetValue(capability, out var entry))
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow();
        if (entry.ExpiresAt <= now)
        {
            _capabilities.TryRemove(capability, out _);
            return false;
        }

        if (!string.Equals(entry.SourceToolSlug, sourceToolSlug, StringComparison.Ordinal))
        {
            return false;
        }

        if (Interlocked.Increment(ref entry.UseCount) > MaximumUses)
        {
            _capabilities.TryRemove(capability, out _);
            return false;
        }

        authenticationContext = entry.AuthenticationContext;
        return true;
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var entry in _capabilities)
        {
            if (entry.Value.ExpiresAt <= now)
            {
                _capabilities.TryRemove(entry.Key, out _);
            }
        }
    }

    private sealed class CapabilityEntry(
        string sourceToolSlug,
        ToolHostAuthenticationContext authenticationContext,
        DateTimeOffset expiresAt)
    {
        public string SourceToolSlug { get; } = sourceToolSlug;
        public ToolHostAuthenticationContext AuthenticationContext { get; } = authenticationContext;
        public DateTimeOffset ExpiresAt { get; } = expiresAt;
        public int UseCount;
    }
}
