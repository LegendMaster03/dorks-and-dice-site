using dorks_and_dice_site.Models.Tools;

namespace dorks_and_dice_site.Services.Tools;

public static class ToolIntegrationContractPolicy
{
    public static bool IsSupported(ToolRegistration tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return GetUnsupportedReason(tool.IntegrationType, tool.IntegrationContractVersion) is null;
    }

    public static string? GetUnsupportedReason(ToolRegistration tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return GetUnsupportedReason(tool.IntegrationType, tool.IntegrationContractVersion);
    }

    public static string? GetUnsupportedReason(
        ToolIntegrationType integrationType,
        int? integrationContractVersion)
    {
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
