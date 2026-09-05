using System.Net;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dorks_and_dice_site.Tests;

public sealed class HomepagePluginIntegrationTests
{
    [Fact]
    public async Task DorksDbHomepageRendersInstalledDiscordPlugin()
    {
        using var factory = new PublishedContentWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using (var scope = factory.Services.CreateScope())
        {
            var authoring = scope.ServiceProvider.GetRequiredService<IContentAuthoringService>();
            var model = authoring.GetNew("External");
            model.Document.Id = "dorks-and-dice-home";
            model.Document.Slug = "dorks-and-dice-home";
            model.Document.TagsText = ContentTags.Homepage;
            model.Document.VisibleModesSelection = [BuiltInSiteModes.DorksAndDice.Id];
            model.Document.Body = """
                # Dorks & Dice Fixture

                {{discord-widget title="Dorks & Dice Discord Server"}}
                """;
            await authoring.CreateAsync(model.Document);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://dorks-and-dice.com/");
        request.Headers.Host = "dorks-and-dice.com";
        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Dorks &amp; Dice Fixture", html, StringComparison.Ordinal);
        Assert.Contains("Dorks &amp; Dice Discord Server", html, StringComparison.Ordinal);
        Assert.Contains("https://discord.com/widget?id=123456789&amp;theme=dark", html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{discord-widget", html, StringComparison.Ordinal);
    }
}
