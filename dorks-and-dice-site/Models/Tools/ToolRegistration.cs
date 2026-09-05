using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Models.Tools;

public enum ToolIntegrationType
{
    EmbeddedModule,
    ProxiedApplication
}

public sealed class ToolRegistration
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ToolIntegrationType IntegrationType { get; set; } = ToolIntegrationType.EmbeddedModule;
    public string? UpstreamBaseUrl { get; set; }
    public string? FrontendEntryPoint { get; set; }
    public string? HealthPath { get; set; }
    public List<string> Modes { get; set; } = [SiteModeValues.DorksAndDiceModeValue];
    public bool AllowAnonymous { get; set; } = true;
    public bool Enabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ToolRegistrationEditViewModel
{
    public Guid? Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ToolIntegrationType IntegrationType { get; set; } = ToolIntegrationType.EmbeddedModule;
    public string? UpstreamBaseUrl { get; set; }
    public string? FrontendEntryPoint { get; set; }
    public string? HealthPath { get; set; }
    public bool DorksAndDiceMode { get; set; } = true;
    public bool ProfessionalMode { get; set; }
    public bool AllowAnonymous { get; set; } = true;
    public bool Enabled { get; set; }
}
