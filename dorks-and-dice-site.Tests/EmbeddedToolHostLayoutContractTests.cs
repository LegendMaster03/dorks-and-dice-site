using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

/// <summary>
/// The shared Site shell exposes the layout contract required by embedded
/// Tool applications without adopting their internal UI implementation.
/// </summary>
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
