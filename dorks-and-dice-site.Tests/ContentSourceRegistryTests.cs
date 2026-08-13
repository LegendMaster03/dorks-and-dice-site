using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Content.Storage;
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
            ["ContentStorage:DevelopmentDefaultSources:0"] = "Local",
            ["ContentStorage:Modes:Professional:InheritGlobal"] = "true",
            ["ContentStorage:Modes:Professional:Add:0"] = "ProfessionalOnly",
            ["ContentStorage:Modes:DorksAndDice:InheritGlobal"] = "true",
            ["ContentStorage:Modes:DorksAndDice:Remove:0"] = "Global",
            ["ContentStorage:Modes:DorksAndDice:Add:0"] = "CommunityOnly",
            // These settings must be ignored by design.
            ["ContentStorage:Modes:Development:Add:0"] = "ProfessionalOnly",
            ["ContentStorage:Modes:Unassigned:Add:0"] = "CommunityOnly"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var registry = new ContentSourceRegistry(configuration, Path.GetTempPath());

        Assert.Equal(
            ["Global", "ProfessionalOnly"],
            registry.GetDefaultSources(SiteMode.Professional).Select(source => source.Key));
        Assert.Equal(
            ["CommunityOnly"],
            registry.GetDefaultSources(SiteMode.DorksAndDice).Select(source => source.Key));
        Assert.Equal(
            ["Local"],
            registry.GetDefaultSources(SiteMode.Development).Select(source => source.Key));
        Assert.Equal(
            ["Global"],
            registry.GetDefaultSources(SiteMode.Unassigned).Select(source => source.Key));
    }

    [Fact]
    public void ManualDevelopmentSelectionCanUseAnyConfiguredSourceSet()
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
            ["ContentStorage:GlobalSources:0"] = "First",
            ["ContentStorage:DevelopmentDefaultSources:0"] = "First"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var registry = new ContentSourceRegistry(configuration, Path.GetTempPath());

        var selected = registry.GetSourcesByKeys(["First", "Second"]);

        Assert.Equal(2, selected.Count);
        Assert.Contains(selected, source => source.Key == "First");
        Assert.Contains(selected, source => source.Key == "Second");
        Assert.Empty(registry.GetSourcesByKeys([]));
    }
}
