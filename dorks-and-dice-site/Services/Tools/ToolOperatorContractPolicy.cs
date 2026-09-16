using dorks_and_dice_site.Models.Tools;

namespace dorks_and_dice_site.Services.Tools;

public static class ToolOperatorContractPolicy
{
    public static string? GetUnsupportedReason(ToolRegistration tool) =>
        GetUnsupportedReason(tool.OperatorContractVersion, tool.OperatorManifestPath);

    public static string? GetUnsupportedReason(int? version, string? manifestPath)
    {
        if (version is null && string.IsNullOrWhiteSpace(manifestPath))
        {
            return "The Tool does not advertise Operator capabilities.";
        }

        if (version != ToolOperatorContractVersions.Current)
        {
            return $"Operator Tool Contract version {version?.ToString() ?? "(none)"} is not supported. The Site currently supports version {ToolOperatorContractVersions.Current}.";
        }

        if (string.IsNullOrWhiteSpace(manifestPath)
            || !manifestPath.StartsWith("/operator/", StringComparison.Ordinal)
            || manifestPath.Contains('?', StringComparison.Ordinal)
            || manifestPath.Contains('#', StringComparison.Ordinal))
        {
            return "Operator manifest path must be an absolute path under /operator/ with no query string or fragment.";
        }

        return null;
    }
}
