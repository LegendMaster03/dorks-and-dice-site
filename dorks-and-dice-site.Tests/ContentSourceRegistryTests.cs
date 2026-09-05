using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ContentSourceRegistryTests
{
    [Fact]
    public void OnlyRealSiteModesApplyModeSpecificSourceDifferences()
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:GlobalDb"] = "Data Source=global.db",
            ["ConnectionStrings:LocalDb"] = "Data Source=local.db",
            ["ConnectionStrings:ProfessionalDb"] = "Data Source=professional.db",
            ["ConnectionStrings:CommunityDb"] = "Data Source=community.db",
            ["ContentStorage:AuthoringSource"] = "Local",
            ["ContentStorage:Sources:Global:Provider"] = "Sqlite",
            ["ContentStorage:Sources:Global:ConnectionString"] = "GlobalDb",
            ["ContentStorage:Sources:Local:Provider"] = "Sqlite",
            ["ContentStorage:Sources:Local:ConnectionString"] = "LocalDb",
            ["ContentStorage:Sources:ProfessionalOnly:Provider"] = "Sqlite",
            ["ContentStorage:Sources:ProfessionalOnly:ConnectionString"] = "ProfessionalDb",
            ["ContentStorage:Sources:CommunityOnly:Provider"] = "Sqlite",
            ["ContentStorage:Sources:CommunityOnly:ConnectionString"] = "CommunityDb",
            ["ContentStorage:GlobalSources:0"] = "Global",
            ["ContentStorage:Modes:professional:InheritGlobal"] = "true",
            ["ContentStorage:Modes:professional:Add:0"] = "ProfessionalOnly",
            ["ContentStorage:Modes:dorks-and-dice:InheritGlobal"] = "true",
            ["ContentStorage:Modes:dorks-and-dice:Remove:0"] = "Global",
            ["ContentStorage:Modes:dorks-and-dice:Add:0"] = "CommunityOnly",
            // Framework states are not normal mode source-composition targets.
            ["ContentStorage:Modes:Development:Add:0"] = "ProfessionalOnly",
            ["ContentStorage:Modes:Unassigned:Add:0"] = "CommunityOnly"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var registry = new ContentSourceRegistry(configuration, Path.GetTempPath());

        Assert.Equal(
            ["Global", "ProfessionalOnly"],
            registry.GetDefaultSources(BuiltInSiteModes.Professional.Id).Select(source => source.Key));
        Assert.Equal(
            ["CommunityOnly"],
            registry.GetDefaultSources(BuiltInSiteModes.DorksAndDice.Id).Select(source => source.Key));

        // Legacy enum callers remain a temporary compatibility path.
        Assert.Equal(
            ["Global", "ProfessionalOnly"],
            registry.GetDefaultSources(SiteMode.Professional).Select(source => source.Key));
        Assert.Empty(registry.GetDefaultSources(SiteMode.Development));
        Assert.Equal(
            ["Global"],
            registry.GetDefaultSources(SiteMode.Unassigned).Select(source => source.Key));
    }

    [Fact]
    public void SyntheticModeCanComposeContentSourcesWithoutLegacyEnum()
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:GlobalDb"] = "Data Source=global.db",
            ["ConnectionStrings:SyntheticDb"] = "Data Source=synthetic.db",
            ["ContentStorage:AuthoringSource"] = "Global",
            ["ContentStorage:Sources:Global:Provider"] = "Sqlite",
            ["ContentStorage:Sources:Global:ConnectionString"] = "GlobalDb",
            ["ContentStorage:Sources:Synthetic:Provider"] = "Sqlite",
            ["ContentStorage:Sources:Synthetic:ConnectionString"] = "SyntheticDb",
            ["ContentStorage:GlobalSources:0"] = "Global",
            ["ContentStorage:Modes:test-mode:Add:0"] = "Synthetic"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var registry = new ContentSourceRegistry(configuration, Path.GetTempPath());

        Assert.Equal(
            ["Global", "Synthetic"],
            registry.GetDefaultSources("test-mode").Select(source => source.Key));
    }

    [Fact]
    public void LegacyBuiltInModeConfigurationRemainsReadableDuringMigration()
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:GlobalDb"] = "Data Source=global.db",
            ["ConnectionStrings:CommunityDb"] = "Data Source=community.db",
            ["ContentStorage:AuthoringSource"] = "Global",
            ["ContentStorage:Sources:Global:Provider"] = "Sqlite",
            ["ContentStorage:Sources:Global:ConnectionString"] = "GlobalDb",
            ["ContentStorage:Sources:Community:Provider"] = "Sqlite",
            ["ContentStorage:Sources:Community:ConnectionString"] = "CommunityDb",
            ["ContentStorage:GlobalSources:0"] = "Global",
            ["ContentStorage:Modes:DorksAndDice:Add:0"] = "Community"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var registry = new ContentSourceRegistry(configuration, Path.GetTempPath());

        Assert.Equal(
            ["Global", "Community"],
            registry.GetDefaultSources(BuiltInSiteModes.DorksAndDice.Id).Select(source => source.Key));
    }

    [Fact]
    public void ManualDevelopmentSelectionCanUseAnyConfiguredSourceSetInRequestedOrder()
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:FirstDb"] = "Data Source=first.db",
            ["ConnectionStrings:SecondDb"] = "Data Source=second.db",
            ["ContentStorage:AuthoringSource"] = "First",
            ["ContentStorage:Sources:First:Provider"] = "Sqlite",
            ["ContentStorage:Sources:First:ConnectionString"] = "FirstDb",
            ["ContentStorage:Sources:Second:Provider"] = "Sqlite",
            ["ContentStorage:Sources:Second:ConnectionString"] = "SecondDb",
            ["ContentStorage:GlobalSources:0"] = "First"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var registry = new ContentSourceRegistry(configuration, Path.GetTempPath());

        var selected = registry.GetSourcesByKeys(["Second", "First", "Second"]);

        Assert.Equal(["Second", "First"], selected.Select(source => source.Key));
        Assert.Empty(registry.GetSourcesByKeys([]));
    }
}
