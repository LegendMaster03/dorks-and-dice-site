using dorks_and_dice_site.Services.Site;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class SiteModeOptionsTests
{
    [Fact]
    public void DeploymentConfigurationMapsHostsByStableModeId()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SiteHosting:Modes:test-mode:CanonicalHost"] = "example.test",
                ["SiteHosting:Modes:test-mode:Domains:0"] = "example.test",
                ["SiteHosting:Modes:test-mode:Domains:1"] = "alias.example.test"
            })
            .Build();

        var options = new SiteModeOptions(configuration);

        Assert.Equal("test-mode", options.ResolveModeId("example.test"));
        Assert.Equal("test-mode", options.ResolveModeId("www.alias.example.test"));
        Assert.Equal("example.test", options.GetCanonicalHost("test-mode"));
    }

    [Fact]
    public void UnknownHostDoesNotBecomeAHostedMode()
    {
        var options = new SiteModeOptions();

        Assert.Null(options.ResolveModeId("unmapped.example.test"));
    }

    [Fact]
    public void CanonicalHostIsAutomaticallyPartOfModeDomainSet()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SiteHosting:Modes:test-mode:CanonicalHost"] = "canonical.example.test",
                ["SiteHosting:Modes:test-mode:Domains:0"] = "alias.example.test"
            })
            .Build();

        var options = new SiteModeOptions(configuration);

        Assert.Equal("test-mode", options.ResolveModeId("canonical.example.test"));
        Assert.Contains("canonical.example.test", options.GetDomains("test-mode"));
    }
}
