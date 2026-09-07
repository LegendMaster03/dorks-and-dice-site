using dorks_and_dice_site.Services.Site;
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
}
