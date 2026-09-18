using System.Text.Json;
using dorks_and_dice_site.Services.Operator;

namespace dorks_and_dice_site.Tests;

public sealed class OperatorOpenApiTests
{
    [Fact]
    public void CapabilityDiscoveryContainsOnlyFrameworkOperatorOperations()
    {
        var registry = new OperatorCapabilityRegistry();
        var capabilities = registry.GetSiteCapabilities();

        Assert.Equal(
            [
                "operator.me",
                "operator.capabilities",
                "operator.browser_bootstrap",
                "operator.openapi"
            ],
            capabilities.Select(value => value.Name).ToArray());

        Assert.All(capabilities, capability =>
        {
            Assert.StartsWith("/operator/v1/", capability.Route, StringComparison.Ordinal);
            Assert.DoesNotContain("/tools/", capability.Route, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void OpenApiMatchesTheRemainingOperatorEndpoints()
    {
        var registry = new OperatorCapabilityRegistry();
        var document = JsonSerializer.SerializeToElement(
            OperatorOpenApiDocument.Create(registry.GetSiteCapabilities()));

        Assert.Equal("3.1.0", document.GetProperty("openapi").GetString());

        var paths = document.GetProperty("paths");
        Assert.Equal(4, paths.EnumerateObject().Count());
        Assert.True(paths.TryGetProperty("/operator/v1/me", out var mePath));
        Assert.True(paths.TryGetProperty("/operator/v1/capabilities", out var capabilitiesPath));
        Assert.True(paths.TryGetProperty("/operator/v1/browser-bootstrap", out var bootstrapPath));
        Assert.True(paths.TryGetProperty("/operator/v1/openapi.json", out var openApiPath));

        AssertOperation(mePath.GetProperty("get"), "operator.me");
        AssertOperation(capabilitiesPath.GetProperty("get"), "operator.capabilities");
        AssertOperation(bootstrapPath.GetProperty("post"), "operator.browser_bootstrap");
        AssertOperation(openApiPath.GetProperty("get"), "operator.openapi");

        var schemas = document
            .GetProperty("components")
            .GetProperty("schemas");
        Assert.Equal(4, schemas.EnumerateObject().Count());
        Assert.True(schemas.TryGetProperty("OperatorMeResponse", out _));
        Assert.True(schemas.TryGetProperty("OperatorCapabilityDescriptor", out var capabilitySchema));
        Assert.True(schemas.TryGetProperty("OperatorCapabilitiesResponse", out _));
        Assert.True(schemas.TryGetProperty("OperatorBrowserBootstrapResponse", out var bootstrapSchema));

        var capabilityProperties = capabilitySchema.GetProperty("properties");
        Assert.Equal(
            ["description", "method", "name", "route"],
            capabilityProperties
                .EnumerateObject()
                .Select(value => value.Name)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());

        var bootstrapProperties = bootstrapSchema.GetProperty("properties");
        Assert.Equal(
            "uri-reference",
            bootstrapProperties.GetProperty("bootstrapUrl").GetProperty("format").GetString());

        Assert.False(schemas.TryGetProperty("OperatorContentWriteRequest", out _));
        Assert.False(schemas.TryGetProperty("ContentAssetInfo", out _));
        Assert.False(schemas.TryGetProperty("ToolOperatorManifest", out _));
    }

    private static void AssertOperation(JsonElement operation, string operationId)
    {
        Assert.Equal(operationId, operation.GetProperty("operationId").GetString());
        Assert.False(operation.TryGetProperty("requestBody", out _));
        Assert.False(operation.TryGetProperty("parameters", out _));

        var responses = operation.GetProperty("responses");
        Assert.Equal(["200", "401"], responses.EnumerateObject().Select(value => value.Name).ToArray());

        var security = Assert.Single(operation.GetProperty("security").EnumerateArray().ToArray());
        Assert.True(security.TryGetProperty("OperatorBearer", out _));
    }
}
