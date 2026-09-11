namespace dorks_and_dice_site.Models.Tools;

public enum ToolIntegrationType
{
    EmbeddedModule,
    ProxiedApplication
}

public enum ToolHealthStatus
{
    NotConfigured,
    Healthy,
    Unhealthy
}

public sealed record ToolHealthResult(
    ToolHealthStatus Status,
    string Detail,
    int? StatusCode,
    long? DurationMilliseconds);

public sealed class ToolRegistration
{
    private bool _allowAnonymous = true;

    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ToolIntegrationType IntegrationType { get; set; } = ToolIntegrationType.EmbeddedModule;
    public string? UpstreamBaseUrl { get; set; }
    public string? FrontendEntryPoint { get; set; }
    public string? HealthPath { get; set; }
    public List<string> Modes { get; set; } = [];

    // Rules Core's published global rules are a public site feature. A persisted legacy value of
    // false must not make the embedded Rules Browser depend on authentication. This exception is
    // deliberately limited to the Embedded Module integration; other tools and integration types
    // continue to honor their configured anonymous-access value.
    public bool AllowAnonymous
    {
        get => (IntegrationType == ToolIntegrationType.EmbeddedModule
                && string.Equals(Slug, "rules-core", StringComparison.OrdinalIgnoreCase))
            || _allowAnonymous;
        set => _allowAnonymous = value;
    }

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
    public List<string> Modes { get; set; } = [];
    public List<ToolModeOptionViewModel> ModeOptions { get; set; } = [];
    public bool AllowAnonymous { get; set; } = true;
    public bool Enabled { get; set; }
}

public sealed class ToolModeOptionViewModel
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}

public sealed class DevelopmentToolListItemViewModel
{
    public required ToolRegistration Tool { get; init; }
    public required ToolHealthResult Health { get; init; }
}
