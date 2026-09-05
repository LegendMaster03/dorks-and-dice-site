using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using dorks_and_dice_site.Services.Tools;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ToolHostingRuntimeTests
{
    [Fact]
    public void ToolVisibilityHonorsSelectedModes()
    {
        var tool = new ToolRegistration
        {
            Modes = [SiteModeValues.ProfessionalModeValue]
        };

        Assert.True(ToolVisibility.IsVisibleInMode(tool, SiteMode.Professional));
        Assert.False(ToolVisibility.IsVisibleInMode(tool, SiteMode.DorksAndDice));
        Assert.False(ToolVisibility.IsVisibleInMode(tool, SiteMode.Unassigned));
    }

    [Fact]
    public void LegacyToolWithoutModesRemainsDorksAndDiceOnly()
    {
        var tool = new ToolRegistration
        {
            Modes = []
        };

        Assert.True(ToolVisibility.IsVisibleInMode(tool, SiteMode.DorksAndDice));
        Assert.False(ToolVisibility.IsVisibleInMode(tool, SiteMode.Professional));
    }

    [Theory]
    [InlineData("/tools/test-tool")]
    [InlineData("/tool-modules/test-tool/app.js")]
    public void ToolRoutesAreModeAdaptive(string path)
    {
        Assert.True(SiteRouteOwnership.IsAllowedInMode(path, SiteMode.DorksAndDice));
        Assert.True(SiteRouteOwnership.IsAllowedInMode(path, SiteMode.Professional));
        Assert.False(SiteRouteOwnership.IsAllowedInMode(path, SiteMode.Unassigned));
    }

    [Theory]
    [InlineData("http://localhost:8123")]
    [InlineData("http://initiative:8080")]
    [InlineData("https://reference-data")]
    public void UpstreamPolicyAllowsLoopbackAndSingleLabelServices(string upstream)
    {
        var policy = CreatePolicy();

        Assert.True(policy.IsAllowed(upstream, out var reason), reason);
    }

    [Theory]
    [InlineData("https://google.com")]
    [InlineData("http://169.254.169.254")]
    [InlineData("http://10.0.0.7:8080")]
    [InlineData("ftp://initiative:21")]
    [InlineData("http://user:password@initiative:8080")]
    public void UpstreamPolicyRejectsUnapprovedExternalOrLiteralHosts(string upstream)
    {
        var policy = CreatePolicy();

        Assert.False(policy.IsAllowed(upstream, out _));
    }

    [Fact]
    public void UpstreamPolicyAllowsExplicitlyConfiguredFqdnOrIp()
    {
        var policy = CreatePolicy(new Dictionary<string, string?>
        {
            ["ToolHosting:AllowedUpstreamHosts:0"] = "tools.internal.example",
            ["ToolHosting:AllowedUpstreamHosts:1"] = "10.0.0.7"
        });

        Assert.True(policy.IsAllowed("https://tools.internal.example:8443", out var fqdnReason), fqdnReason);
        Assert.True(policy.IsAllowed("http://10.0.0.7:8080", out var ipReason), ipReason);
    }

    [Fact]
    public void UpstreamUriRejectsDecodedPathTraversal()
    {
        var tool = new ToolRegistration
        {
            UpstreamBaseUrl = "http://initiative:8080"
        };

        Assert.False(ToolUpstreamUri.TryBuild(tool, "/%2e%2e/secrets", QueryString.Empty, out _));
    }

    [Fact]
    public void UpstreamUriPreservesSafeQueryString()
    {
        var tool = new ToolRegistration
        {
            UpstreamBaseUrl = "http://initiative:8080"
        };

        Assert.True(ToolUpstreamUri.TryBuild(tool, "/app.js", new QueryString("?v=123"), out var uri));
        Assert.Equal("http://initiative:8080/app.js?v=123", uri?.ToString());
    }

    private static ToolUpstreamPolicy CreatePolicy(Dictionary<string, string?>? values = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();

        return new ToolUpstreamPolicy(configuration);
    }
}
