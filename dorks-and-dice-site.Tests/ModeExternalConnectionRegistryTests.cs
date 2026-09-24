using dorks_and_dice_site.Services.Site;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ModeExternalConnectionRegistryTests
{
    [Fact]
    public void ConnectionsAreScopedByModeAndMayShareExternalResource()
    {
        const string sharedGuildId = "1281714470799806545";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ModeConnections:dorks-and-dice:discord:ResourceId"] = sharedGuildId,
                ["ModeConnections:professional:discord:ResourceId"] = sharedGuildId
            })
            .Build();
        var modes = new SiteModeRegistry(BuiltInSiteModes.All);

        var registry = new ConfigurationModeExternalConnectionRegistry(
            configuration,
            modes);

        Assert.Equal(2, registry.All.Count);
        Assert.True(registry.TryGet("dorks-and-dice", "discord", out var dorks));
        Assert.True(registry.TryGet("professional", "DISCORD", out var professional));
        Assert.Equal(sharedGuildId, dorks!.ResourceId);
        Assert.Equal(sharedGuildId, professional!.ResourceId);
        Assert.NotEqual(dorks.ModeId, professional.ModeId);
    }

    [Fact]
    public void UnknownModeConnectionIsRejected()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ModeConnections:not-a-mode:discord:ResourceId"] = "123456789"
            })
            .Build();
        var modes = new SiteModeRegistry(BuiltInSiteModes.All);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ConfigurationModeExternalConnectionRegistry(configuration, modes));

        Assert.Contains("unknown site mode", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProviderResourceIdIsRequired()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ModeConnections:dorks-and-dice:discord:Enabled"] = "true"
            })
            .Build();
        var modes = new SiteModeRegistry(BuiltInSiteModes.All);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ConfigurationModeExternalConnectionRegistry(configuration, modes));

        Assert.Contains("ResourceId", exception.Message, StringComparison.Ordinal);
    }
}
