using dorks_and_dice_site.Framework.Operator;

namespace dorks_and_dice_site.Models.Operator;

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
