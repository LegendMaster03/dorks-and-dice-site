using System.Net;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Plugins.MinecraftServerStatus;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Hosting;
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

                {{discord-widget server-id="123456789" theme="dark" title="Dorks & Dice Discord Server"}}
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

    [Fact]
    public async Task DorksDbHomepageRendersInstalledMinecraftStatusPlugin()
    {
        using var rootFactory = new PublishedContentWebApplicationFactory();
        using var factory = WithMinecraftSnapshot(rootFactory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using (var scope = factory.Services.CreateScope())
        {
            var authoring = scope.ServiceProvider.GetRequiredService<IContentAuthoringService>();
            var model = authoring.GetNew("External");
            model.Document.Id = "dorks-and-dice-home-minecraft";
            model.Document.Slug = "dorks-and-dice-home-minecraft";
            model.Document.TagsText = ContentTags.Homepage;
            model.Document.VisibleModesSelection = [BuiltInSiteModes.DorksAndDice.Id];
            model.Document.Body = """
                # Dorks & Dice Fixture

                ### Minecraft

                Server launched October 17, 2025.

                {{minecraft-server-status}}
                """;
            await authoring.CreateAsync(model.Document);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://dorks-and-dice.com/");
        request.Headers.Host = "dorks-and-dice.com";
        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Minecraft", html, StringComparison.Ordinal);
        Assert.Contains("Server launched October 17, 2025.", html, StringComparison.Ordinal);
        Assert.Contains("Online", html, StringComparison.Ordinal);
        Assert.Contains("The Fools", html, StringComparison.Ordinal);
        Assert.Contains("3", html, StringComparison.Ordinal);
        Assert.Contains("20", html, StringComparison.Ordinal);
        Assert.Contains("Version 26.2", html, StringComparison.Ordinal);
        Assert.Contains("data-minecraft-status-field=\"badge\"", html, StringComparison.Ordinal);
        Assert.Contains("/plugins/minecraft-server-status/status.js", html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{minecraft-server-status", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DorksDbHomepageCanComposeMinecraftStatusFieldsIndependently()
    {
        using var rootFactory = new PublishedContentWebApplicationFactory();
        using var factory = WithMinecraftSnapshot(rootFactory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using (var scope = factory.Services.CreateScope())
        {
            var authoring = scope.ServiceProvider.GetRequiredService<IContentAuthoringService>();
            var model = authoring.GetNew("External");
            model.Document.Id = "dorks-and-dice-home-minecraft-fields";
            model.Document.Slug = "dorks-and-dice-home-minecraft-fields";
            model.Document.TagsText = ContentTags.Homepage;
            model.Document.VisibleModesSelection = [BuiltInSiteModes.DorksAndDice.Id];
            model.Document.Body = """
                # Dorks & Dice Fixture

                ::: {.d-flex .justify-content-between}

                ### Minecraft

                {{minecraft-server-status-badge}}

                :::

                Server launched October 17, 2025.

                {{minecraft-server-motd}}

                {{minecraft-server-online-players}}

                {{minecraft-server-maximum-players}}

                {{minecraft-server-players}}

                {{minecraft-server-version}}
                """;
            await authoring.CreateAsync(model.Document);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://dorks-and-dice.com/");
        request.Headers.Host = "dorks-and-dice.com";
        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-minecraft-status-field=\"badge\"", html, StringComparison.Ordinal);
        Assert.Contains("data-minecraft-status-field=\"motd\"", html, StringComparison.Ordinal);
        Assert.Contains("data-minecraft-status-field=\"online-players\"", html, StringComparison.Ordinal);
        Assert.Contains("data-minecraft-status-field=\"maximum-players\"", html, StringComparison.Ordinal);
        Assert.Contains("data-minecraft-status-field=\"players\"", html, StringComparison.Ordinal);
        Assert.Contains("data-minecraft-status-field=\"version\"", html, StringComparison.Ordinal);
        Assert.Contains("The Fools", html, StringComparison.Ordinal);
        Assert.Contains("Version 26.2", html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{minecraft-server-", html, StringComparison.Ordinal);

        using var snapshotRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "https://dorks-and-dice.com/plugins/minecraft-server-status/snapshot");
        snapshotRequest.Headers.Host = "dorks-and-dice.com";
        var snapshotResponse = await client.SendAsync(snapshotRequest);
        var json = await snapshotResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);
        Assert.Contains("\"isOnline\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"motd\":\"The Fools\"", json, StringComparison.Ordinal);
        Assert.Contains("\"onlinePlayers\":3", json, StringComparison.Ordinal);
        Assert.Contains("\"maximumPlayers\":20", json, StringComparison.Ordinal);
        Assert.Contains("\"version\":\"26.2\"", json, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> WithMinecraftSnapshot(
        PublishedContentWebApplicationFactory rootFactory) =>
        rootFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var existing = services.Single(
                    descriptor => descriptor.ServiceType == typeof(IMinecraftServerStatusSnapshotStore));
                services.Remove(existing);
                services.AddSingleton<IMinecraftServerStatusSnapshotStore>(new StubMinecraftServerStatusSnapshotStore());
            });
        });

    private sealed class StubMinecraftServerStatusSnapshotStore : IMinecraftServerStatusSnapshotStore
    {
        public MinecraftServerStatus Current { get; } = new(
            IsOnline: true,
            Motd: "The Fools",
            Version: "26.2",
            OnlinePlayers: 3,
            MaximumPlayers: 20,
            CheckedAt: DateTimeOffset.UtcNow);
    }
}
