namespace dorks_and_dice_site.Services.Tools;

public static class ToolPrivateTunnelHeaders
{
    public const string ReservedPrefix = "X-Dorks-Tool-Private-Tunnel-";
    public const string Capability = "X-Dorks-Tool-Private-Tunnel-Capability";
    public const string TicketPath = "X-Dorks-Tool-Private-Tunnel-Ticket-Path";
}

/// <summary>
/// Deployment-level allowlist for direct private Tool-to-Tool connections. This policy is
/// intentionally separate from ToolRegistration.DelegationTargets: ordinary delegation does not
/// grant private API access, and private tunnel access does not imply Site-proxied delegation.
///
/// Configuration shape:
/// ToolHosting:PrivateTunnels:{sourceToolKey}:0 = {targetToolKey}
/// ToolHosting:PrivateTunnels:{sourceToolKey}:1 = {anotherTargetToolKey}
/// </summary>
public static class ToolPrivateTunnelPolicy
{
    public const string ConfigurationSection = "ToolHosting:PrivateTunnels";

    public static bool HasTargets(IConfiguration configuration, string sourceToolKey) =>
        GetTargets(configuration, sourceToolKey).Count > 0;

    public static bool Allows(
        IConfiguration configuration,
        string sourceToolKey,
        string targetToolKey) =>
        GetTargets(configuration, sourceToolKey)
            .Contains(targetToolKey, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> GetTargets(
        IConfiguration configuration,
        string sourceToolKey)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (string.IsNullOrWhiteSpace(sourceToolKey))
        {
            return [];
        }

        return configuration
            .GetSection(ConfigurationSection)
            .GetChildren()
            .FirstOrDefault(section => string.Equals(
                section.Key,
                sourceToolKey,
                StringComparison.OrdinalIgnoreCase))?
            .GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray()
            ?? [];
    }
}
