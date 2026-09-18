using dorks_and_dice_site.Framework.Operator;

namespace dorks_and_dice_site.Services.Operator;

public sealed class OperatorCapabilityRegistry : IOperatorCapabilityRegistry
{
    private static readonly IReadOnlyList<OperatorCapabilityDescriptor> Capabilities =
    [
        new(
            "operator.me",
            "GET",
            "/operator/v1/me",
            "Return the authenticated service-principal identity and effective global roles."),
        new(
            "operator.capabilities",
            "GET",
            "/operator/v1/capabilities",
            "Discover framework-owned Operator operations."),
        new(
            "operator.browser_bootstrap",
            "POST",
            "/operator/v1/browser-bootstrap",
            "Issue a short-lived one-use URL that establishes a normal Site browser session."),
        new(
            "operator.openapi",
            "GET",
            "/operator/v1/openapi.json",
            "Return the Operator HTTPS/JSON OpenAPI description.")
    ];

    public IReadOnlyList<OperatorCapabilityDescriptor> GetSiteCapabilities() => Capabilities;
}
