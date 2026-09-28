using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class WebXrFrameworkIntegrationTests(PublishedContentWebApplicationFactory factory)
{
    [Fact]
    public async Task ResponsesPermitSameOriginXrSpatialTracking()
    {
        var response = await SendAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Permissions-Policy", out var values));

        var policy = string.Join(", ", values);
        Assert.Contains("camera=()", policy, StringComparison.Ordinal);
        Assert.Contains("microphone=()", policy, StringComparison.Ordinal);
        Assert.Contains("geolocation=()", policy, StringComparison.Ordinal);
        Assert.Contains("xr-spatial-tracking=(self)", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("xr-spatial-tracking=(*)", policy, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/js/xr/xr-capabilities.js", "detectXrCapabilities")]
    [InlineData("/js/xr/xr-device-profiles.js", "XrDeviceProfileRegistry")]
    [InlineData("/js/xr/xr-input.js", "XrInputManager")]
    [InlineData("/js/xr/xr-runtime.js", "createXrRuntime")]
    [InlineData("/js/xr/xr-diagnostics.js", "installXrDiagnosticsButton")]
    public async Task WebXrModulesArePublishedAsStaticAssets(string path, string expectedExport)
    {
        var response = await SendAsync(path);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedExport, content, StringComparison.Ordinal);
        Assert.NotEmpty(content);
    }

    [Fact]
    public async Task RuntimePinsThreeJsWithoutLoadingItFromTheSharedLayout()
    {
        var runtimeResponse = await SendAsync("/js/xr/xr-runtime.js");
        var runtime = await runtimeResponse.Content.ReadAsStringAsync();
        var homeResponse = await SendAsync("/");
        var home = await homeResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, runtimeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);
        Assert.Contains(
            "https://cdn.jsdelivr.net/npm/three@0.186.1/build/three.module.js",
            runtime,
            StringComparison.Ordinal);
        Assert.DoesNotContain("three.module.js", home, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/js/xr/xr-runtime.js", home, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HttpResponseMessage> SendAsync(string path)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://dorks-and-dice.com{path}");
        request.Headers.Host = "dorks-and-dice.com";
        return await client.SendAsync(request);
    }
}
