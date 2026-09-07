using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class SiteModeRegistrationSourceIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public SiteModeRegistrationSourceIntegrationTests(PublishedContentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void RuntimeRegistryIsComposedFromRegistrationSource()
    {
        using var scope = _factory.Services.CreateScope();
        var source = scope.ServiceProvider.GetRequiredService<ISiteModeRegistrationSource>();
        var registry = scope.ServiceProvider.GetRequiredService<ISiteModeRegistry>();

        Assert.IsType<DeploymentSiteModeRegistrationSource>(source);
        Assert.Equal(
            source.GetDefinitions().Select(definition => definition.Id),
            registry.All.Select(definition => definition.Id));
    }

    [Fact]
    public void LegacyBuiltInFacadeDelegatesToDeploymentRegistrationSource()
    {
        var definitions = new DeploymentSiteModeRegistrationSource().GetDefinitions();

        Assert.Equal(
            definitions.Select(definition => definition.Id),
            BuiltInSiteModes.All.Select(definition => definition.Id));
        Assert.Same(
            definitions.Single(definition => definition.LegacyMode == SiteMode.DorksAndDice),
            BuiltInSiteModes.DorksAndDice);
        Assert.Same(
            definitions.Single(definition => definition.LegacyMode == SiteMode.Professional),
            BuiltInSiteModes.Professional);
    }

    [Fact]
    public void DeploymentCanAddAThirdNormalModeByReplacingOnlyTheRegistrationSource()
    {
        var extraMode = new SiteModeDefinition(
            Id: "portable-test",
            DisplayName: "Portable Test",
            LegacyMode: null,
            ViewFolder: "PortableTest",
            AssetFolder: "portable-test");
        var definitions = new DeploymentSiteModeRegistrationSource()
            .GetDefinitions()
            .Append(extraMode)
            .ToArray();

        using var customizedFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<ISiteModeRegistrationSource>(new TestRegistrationSource(definitions))));
        using var scope = customizedFactory.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<ISiteModeRegistry>();

        Assert.Same(extraMode, registry.GetById(extraMode.Id));
        Assert.Equal(3, registry.All.Count);
    }

    private sealed class TestRegistrationSource(IReadOnlyList<SiteModeDefinition> definitions)
        : ISiteModeRegistrationSource
    {
        public IReadOnlyList<SiteModeDefinition> GetDefinitions() => definitions;
    }
}
