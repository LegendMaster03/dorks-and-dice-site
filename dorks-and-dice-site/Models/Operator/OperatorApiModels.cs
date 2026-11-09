using dorks_and_dice_site.Framework.Operator;

namespace dorks_and_dice_site.Models.Operator;

/// <summary>
/// Identity response for a credential-authenticated Operator service principal.
/// It is distinct from a human browser login session.
/// </summary>
public sealed record OperatorMeResponse(
    Guid UserId,
    string DisplayName,
    string AccountKind,
    IReadOnlyList<string> GlobalRoles);

public sealed record OperatorCapabilitiesResponse(
    IReadOnlyList<OperatorCapabilityDescriptor> Site);

public sealed record OperatorBrowserBootstrapResponse(
    Guid BootstrapId,
    string BootstrapUrl,
    DateTimeOffset ExpiresAt);
