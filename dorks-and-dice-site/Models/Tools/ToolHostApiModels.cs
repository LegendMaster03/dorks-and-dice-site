using System.Text.Json.Serialization;

namespace dorks_and_dice_site.Models.Tools;

public sealed class ToolHostApiSession
{
    public int ContractVersion { get; init; } = 1;
    public string ToolKey { get; init; } = string.Empty;
    public required string ToolSlug { get; init; }
    public required string SiteMode { get; init; }
    public required ToolHostUserContext User { get; init; }
    public IReadOnlyList<string> GlobalRoles { get; init; } = [];
}

/// <summary>
/// Campaign authorization summary exposed through the Tool Host contract. This projection is
/// independent of Dorks & Dice campaign persistence and exists only as a Tool integration DTO.
/// Contract version 1 carries one campaign role per entry.
/// </summary>
public sealed class ToolHostCampaignAccessSummary
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Role { get; init; }
}

/// <summary>
/// Owner-only Character authorization summary exposed to Tool backends that need Site-owned
/// Character identity. CampaignIds contains only current active Character-to-campaign associations.
/// </summary>
public sealed class ToolHostCharacterAccessSummary
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset? ArchivedAt { get; init; }
    public IReadOnlyList<Guid> CampaignIds { get; init; } = [];
}

/// <summary>
/// Authoritative identity/authorization context redeemed by a Tool backend from a short-lived
/// host-issued authentication ticket. Source-content grants intentionally do not live here;
/// those remain owned by the Tool that stores the protected content.
/// </summary>
public sealed class ToolHostAuthenticationContext
{
    public int ContractVersion { get; init; } = 1;
    public string ToolKey { get; init; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolSlug { get; init; }

    /// <summary>
    /// Immediate ordinary Tool-to-Tool delegation source for target-scoped contexts. These fields
    /// are omitted for normal browser-to-Tool and private-tunnel contexts.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DelegatedFromToolKey { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DelegatedFromToolSlug { get; init; }

    /// <summary>
    /// Immediate source Tool for a target-scoped private-tunnel context. Site emits these fields
    /// only after the configured private source-to-target relationship has been authorized. They
    /// are distinct from ordinary Tool delegation provenance.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrivateTunnelSourceToolKey { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrivateTunnelSourceToolSlug { get; init; }

    public required string SiteMode { get; init; }
    public required ToolHostUserContext User { get; init; }
    public IReadOnlyList<string> GlobalRoles { get; init; } = [];

    /// <summary>
    /// Effective account roles scoped to SiteMode. This additive contract-v1 field is always
    /// emitted by current Site versions; older Site versions omit it.
    /// </summary>
    public IReadOnlyList<string> ScopedRoles { get; init; } = [];

    public IReadOnlyList<ToolHostCampaignAccessSummary> Campaigns { get; init; } = [];

    /// <summary>
    /// Optional Tool-specific Character ownership projection. It is populated for Character Sheet
    /// and omitted for Tools that do not need Character ownership so their version-1 payload stays
    /// unchanged apart from additive fields and the stable registration key.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ToolHostCharacterAccessSummary>? Characters { get; init; }

    [JsonIgnore]
    public string RegistrationKey =>
        !string.IsNullOrWhiteSpace(ToolKey)
            ? ToolKey
            : ToolSlug ?? string.Empty;
}

public sealed class ToolPrivateTunnelTicketResponse
{
    public required string TargetKey { get; init; }
    public required string Ticket { get; init; }
    public required string IntrospectionPath { get; init; }
}
