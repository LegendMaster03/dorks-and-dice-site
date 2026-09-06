using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ContentAuthoringNavigationTests
{
    [Fact]
    public void HomepagePublicRouteUsesModeRoot()
    {
        Assert.Equal(
            "/",
            ContentPublicRoute.GetPath("professional-home", [ContentTags.Homepage]));
    }

    [Fact]
    public void UnsavedContentHasNoPublicRouteYet()
    {
        Assert.Equal(string.Empty, ContentPublicRoute.GetPath(string.Empty, [ContentTags.Article]));
    }

    [Fact]
    public void ExperienceOnlyPublicRoutePreservesExperienceContext()
    {
        Assert.Equal(
            "/resume/cybersecurity?context=experience",
            ContentPublicRoute.GetPath("cybersecurity", [ContentTags.Experience]));
    }

    [Fact]
    public void PreviewVisibleExternalSourceIsAvailableAlongsideAuthoringWorkspace()
    {
        using var fixture = new SourceFixture();
        var context = new SiteModeContext
        {
            ActiveMode = BuiltInSiteModes.Professional,
            IsDevelopmentPreview = true,
            HasContentSourceOverride = true,
            EnabledContentSources = new HashSet<string>(["External"], StringComparer.OrdinalIgnoreCase)
        };

        var sources = ContentAuthoringSourceAccess.GetAccessibleSources(fixture.Registry, context);

        Assert.Equal(["Local", "External"], sources.Select(source => source.Key));
        Assert.Equal(
            "External",
            ContentAuthoringSourceAccess.ResolveAccessibleSourceKey(fixture.Registry, context, "external"));
        Assert.Throws<InvalidOperationException>(() =>
            ContentAuthoringSourceAccess.ResolveAccessibleSourceKey(fixture.Registry, context, "Hidden"));
    }

    private sealed class SourceFixture : IDisposable
    {
        private readonly string _directory;

        public SourceFixture()
        {
            _directory = Path.Combine(Path.GetTempPath(), $"content-authoring-navigation-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:LocalDb"] = "Data Source=local.db",
                ["ConnectionStrings:ExternalDb"] = "Data Source=external.db",
                ["ConnectionStrings:HiddenDb"] = "Data Source=hidden.db",
                ["ContentStorage:AuthoringSource"] = "Local",
                ["ContentStorage:Sources:Local:DisplayName"] = "Local content",
                ["ContentStorage:Sources:Local:Provider"] = "Sqlite",
                ["ContentStorage:Sources:Local:ConnectionString"] = "LocalDb",
                ["ContentStorage:Sources:External:DisplayName"] = "External content",
                ["ContentStorage:Sources:External:Provider"] = "Sqlite",
                ["ContentStorage:Sources:External:ConnectionString"] = "ExternalDb",
                ["ContentStorage:Sources:Hidden:DisplayName"] = "Hidden content",
                ["ContentStorage:Sources:Hidden:Provider"] = "Sqlite",
                ["ContentStorage:Sources:Hidden:ConnectionString"] = "HiddenDb",
                ["ContentStorage:GlobalSources:0"] = "External",
                ["ContentStorage:ModeSources:professional:0"] = "External"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
            Registry = new ContentSourceRegistry(configuration, _directory);
        }

        public ContentSourceRegistry Registry { get; }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
