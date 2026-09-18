using System.Text.Json;
using dorks_and_dice_site.Services.Operator;
using Microsoft.AspNetCore.Http;

namespace dorks_and_dice_site.Tests;

public sealed class OperatorOpenApiTests
{
    [Fact]
    public void CapabilityDiscoveryIsFrameworkOwnedAndIncludesBrowserBootstrap()
    {
        var registry = new OperatorCapabilityRegistry();
        var capabilities = registry.GetSiteCapabilities();

        Assert.DoesNotContain(capabilities, value => value.Route.Contains("/tools/", StringComparison.Ordinal));
        Assert.DoesNotContain(capabilities, value => value.Name.StartsWith("tools.", StringComparison.Ordinal));

        var bootstrap = capabilities.Single(value => value.Name == "operator.browser_bootstrap");
        Assert.Equal("POST", bootstrap.Method);
        Assert.Equal("/operator/v1/browser-bootstrap", bootstrap.Route);
        Assert.Equal("OperatorBrowserBootstrapResponse", bootstrap.ResponseSchema);

        var create = capabilities.Single(value => value.Name == "content.create");
        Assert.Equal("OperatorContentWriteRequest", create.RequestSchema);
        Assert.Equal("OperatorContentDocumentResponse", create.ResponseSchema);
        Assert.Equal(StatusCodes.Status201Created, create.SuccessStatusCode);

        var document = JsonSerializer.SerializeToElement(OperatorOpenApiDocument.Create(capabilities));
        Assert.Equal("3.1.0", document.GetProperty("openapi").GetString());

        var capabilityResponse = document
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("OperatorCapabilitiesResponse");
        var capabilityProperties = capabilityResponse.GetProperty("properties");
        Assert.True(capabilityProperties.TryGetProperty("site", out _));
        Assert.False(capabilityProperties.TryGetProperty("tools", out _));

        var bootstrapOperation = document
            .GetProperty("paths")
            .GetProperty("/operator/v1/browser-bootstrap")
            .GetProperty("post");
        Assert.True(bootstrapOperation.GetProperty("responses").TryGetProperty("200", out _));

        var schemas = document.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.TryGetProperty("OperatorBrowserBootstrapResponse", out _));
        Assert.False(schemas.TryGetProperty("OperatorToolSummary", out _));
        Assert.False(schemas.TryGetProperty("ToolOperatorManifest", out _));

        var createOperation = document
            .GetProperty("paths")
            .GetProperty("/operator/v1/content")
            .GetProperty("post");
        Assert.True(createOperation.GetProperty("responses").TryGetProperty("201", out _));
        Assert.Equal(
            "#/components/schemas/OperatorContentWriteRequest",
            createOperation
                .GetProperty("requestBody")
                .GetProperty("content")
                .GetProperty("application/json")
                .GetProperty("schema")
                .GetProperty("$ref")
                .GetString());
    }
}
