using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class EmbeddedToolHostLayoutContractTests(PublishedContentWebApplicationFactory factory)
{
    [Fact]
    public async Task SiteShellExposesFullBleedContentLayoutContract()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://dorks-and-dice.com")
        });

        var html = await client.GetStringAsync("/");

        Assert.Contains("id=\"site-content-shell\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"site-footer\"", html, StringComparison.Ordinal);
        Assert.Contains("dorksAndDiceToolHost", html, StringComparison.Ordinal);
        Assert.Contains("setContentLayout(layout)", html, StringComparison.Ordinal);
        Assert.Contains("full-bleed", html, StringComparison.Ordinal);
    }
}
