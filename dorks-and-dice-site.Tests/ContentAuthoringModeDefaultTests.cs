using dorks_and_dice_site.Models.Site;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Tests;

public sealed class ContentAuthoringModeDefaultTests
{
    [Fact]
    public void NewContentDefaultsToFirstRegisteredModeWithoutBuiltInModeKnowledge()
    {
        var first = new SiteModeDefinition(
            Id: "portable-first",
            DisplayName: "Portable First",
            LegacyMode: null,
            ViewFolder: "PortableFirst",
            AssetFolder: "portable-first");
        var second = new SiteModeDefinition(
            Id: "portable-second",
            DisplayName: "Portable Second",
            LegacyMode: null,
            ViewFolder: "PortableSecond",
            AssetFolder: "portable-second");
        var service = new ContentAuthoringService(
            new StubSourceRegistry(),
            new SiteModeRegistry([first, second]));

        var model = service.GetNew("Test");

        Assert.Equal([first.Id], model.Document.VisibleModesSelection);
        Assert.Equal(first.Id, model.Document.VisibleModesText);
        Assert.Equal([first.Id, second.Id], model.Modes.Select(mode => mode.Id));
    }

    private sealed class StubSourceRegistry : IContentSourceRegistry
    {
        private static readonly ContentSourceDefinition Source = new(
            "Test",
            "Test",
            "Sqlite",
            "Data Source=:memory:");

        public string AuthoringSourceKey => Source.Key;

        public IReadOnlyList<ContentSourceDefinition> GetDefaultSources(string modeId) => [Source];
        public IReadOnlyList<ContentSourceDefinition> GetDefaultSources(SiteMode siteMode) => [Source];
        public IReadOnlyList<ContentSourceDefinition> GetSourcesByKeys(IEnumerable<string> keys) => [Source];
        public IReadOnlyList<ContentSourceDefinition> GetAllSources() => [Source];
        public IReadOnlyList<ContentSourceDefinition> GetGlobalSources() => [Source];
        public bool IsGlobalSource(string sourceKey) => string.Equals(sourceKey, Source.Key, StringComparison.OrdinalIgnoreCase);
        public ContentSourceDefinition GetSource(string key) =>
            string.Equals(key, Source.Key, StringComparison.OrdinalIgnoreCase)
                ? Source
                : throw new InvalidOperationException();
        public IReadOnlySet<string> GetKnownSourceKeys() => new HashSet<string>([Source.Key], StringComparer.OrdinalIgnoreCase);
        public void ConfigureDbContext(DbContextOptionsBuilder options, string sourceKey) => options.UseSqlite(Source.ConnectionString);
    }
}
