using System.Text.Json;
using dorks_and_dice_site.Services.Operator;
using Microsoft.AspNetCore.Http;

namespace dorks_and_dice_site.Tests;

public sealed class OperatorOpenApiTests
{
    [Fact]
    public void CapabilityDiscoveryAndOpenApiExposeRequestAndResponseSchemas()
    {
        var registry = new OperatorCapabilityRegistry();
        var capabilities = registry.GetSiteCapabilities();

        var create = capabilities.Single(value => value.Name == "content.create");
        Assert.Equal("OperatorContentWriteRequest", create.RequestSchema);
        Assert.Equal("OperatorContentDocumentResponse", create.ResponseSchema);
        Assert.Equal(StatusCodes.Status201Created, create.SuccessStatusCode);

        var document = JsonSerializer.SerializeToElement(OperatorOpenApiDocument.Create(capabilities));
        Assert.Equal("3.1.0", document.GetProperty("openapi").GetString());

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

        var schemas = document.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.TryGetProperty("OperatorContentWriteRequest", out _));
        Assert.True(schemas.TryGetProperty("OperatorContentDocumentResponse", out _));

        var invoke = document
            .GetProperty("paths")
            .GetProperty("/operator/v1/tools/{slug}/invoke/{capability}")
            .GetProperty("post");
        var parameters = invoke.GetProperty("parameters").EnumerateArray().ToArray();
        Assert.Contains(parameters, value => value.GetProperty("name").GetString() == "slug");
        Assert.Contains(parameters, value => value.GetProperty("name").GetString() == "capability");
    }
}
