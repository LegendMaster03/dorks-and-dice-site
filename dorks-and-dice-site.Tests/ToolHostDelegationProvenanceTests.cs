using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class ToolHostDelegationProvenanceTests(PublishedContentWebApplicationFactory factory)
{
    [Fact]
    public async Task DelegatedTargetContextIdentifiesImmediateSourceTool()
    {
        using var scope = factory.Services.CreateScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IToolHostAuthenticationContextFactory>();

        var source = new ToolHostAuthenticationContext
        {
            ToolKey = "rules-wiki",
            ToolSlug = "rules-wiki",
            SiteMode = "dorks-and-dice",
            User = new ToolHostUserContext
            {
                Id = Guid.NewGuid().ToString("D"),
                DisplayName = "Rules Wiki User"
            }
        };
        var target = new ToolRegistration
        {
            Key = "rules-core",
            Slug = "rules-core"
        };

        var delegated = await contextFactory.CreateDelegatedAsync(target, source);

        Assert.NotNull(delegated);
        Assert.Equal("rules-core", delegated.ToolKey);
        Assert.Equal("rules-core", delegated.ToolSlug);
        Assert.Equal("rules-wiki", delegated.DelegatedFromToolKey);
        Assert.Equal("rules-wiki", delegated.DelegatedFromToolSlug);
        Assert.Null(source.DelegatedFromToolKey);
        Assert.Null(source.DelegatedFromToolSlug);
    }
}
