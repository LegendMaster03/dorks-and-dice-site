using dorks_and_dice_site.Models.Tools;

namespace dorks_and_dice_site.Services.Tools;

/// <summary>
/// Rejects unsupported Embedded Module frontend contract versions while
/// allowing headless services and non-embedded application registrations
/// to follow their own integration contracts.
/// </summary>
public static class ToolIntegrationContractPolicy
{
    public static bool IsSupported(ToolRegistration tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return GetUnsupportedReason(tool) is null;
    }

    public static string? GetUnsupportedReason(ToolRegistration tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return GetUnsupportedReason(tool.Kind, tool.IntegrationType, tool.IntegrationContractVersion);
    }

    public static string? GetUnsupportedReason(
        ToolIntegrationType integrationType,
        int? integrationContractVersion) =>
        GetUnsupportedReason(ToolKind.Application, integrationType, integrationContractVersion);

    public static string? GetUnsupportedReason(
        ToolKind kind,
        ToolIntegrationType? integrationType,
        int? integrationContractVersion)
    {
        if (kind == ToolKind.Service)
        {
            return null;
        }

        if (!integrationType.HasValue)
        {
            return "Application hosting integration type is required.";
        }

        if (integrationType != ToolIntegrationType.EmbeddedModule)
        {
            return null;
        }

        if (integrationContractVersion == ToolIntegrationContractVersions.EmbeddedModuleCurrent)
        {
            return null;
        }

        return integrationContractVersion.HasValue
            ? $"Embedded Module integration contract version {integrationContractVersion.Value} is unsupported. Supported version is {ToolIntegrationContractVersions.EmbeddedModuleCurrent}."
            : $"Embedded Module integration contract version is required. Supported version is {ToolIntegrationContractVersions.EmbeddedModuleCurrent}.";
    }
}
