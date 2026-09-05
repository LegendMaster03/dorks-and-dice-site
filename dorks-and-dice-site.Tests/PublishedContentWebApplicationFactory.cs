using System.Text.Json;
using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace dorks_and_dice_site.Tests;

[CollectionDefinition(Name)]
public sealed class PublishedContentIntegrationCollection : ICollectionFixture<PublishedContentWebApplicationFactory>
{
    public const string Name = "Published content integration";
}

/// <summary>
/// Gives HTTP integration tests deterministic published content without coupling
/// them to either the empty Local authoring workspace or the live External store.
/// </summary>
public sealed class PublishedContentWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), $"dorks-and-dice-integration-{Guid.NewGuid():N}");
    private int _seeded;

    public string ToolRegistryPath => Path.Combine(_directory, "tool-registry.json");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_directory);
        var externalPath = Path.Combine(_directory, "external.db");
        var localPath = Path.Combine(_directory, "local.db");
        var identityPath = Path.Combine(_directory, "identity.db");

        builder.UseSetting("ConnectionStrings:ContentDatabaseExternal", $"Data Source={externalPath};Pooling=False");
        builder.UseSetting("ConnectionStrings:ContentDatabaseLocal", $"Data Source={localPath};Pooling=False");
        builder.UseSetting("ContentStorage:AuthoringSource", "External");
        builder.UseSetting("ContentStorage:Sources:External:DisplayName", "External content");
        builder.UseSetting("ContentStorage:Sources:External:Provider", "Sqlite");
        builder.UseSetting("ContentStorage:Sources:External:ConnectionString", "ContentDatabaseExternal");
        builder.UseSetting("ContentStorage:Sources:Local:DisplayName", "Local content");
        builder.UseSetting("ContentStorage:Sources:Local:Provider", "Sqlite");
        builder.UseSetting("ContentStorage:Sources:Local:ConnectionString", "ContentDatabaseLocal");
        builder.UseSetting("ContentStorage:GlobalSources:0", "External");
        builder.UseSetting("ConnectionStrings:IdentityDatabase", $"Data Source={identityPath};Pooling=False");
        builder.UseSetting("IdentityStorage:Provider", "Sqlite");
        builder.UseSetting("IdentityStorage:ApplyMigrationsOnStartup", "false");
        builder.UseSetting("IdentityStorage:EnsureCreatedOnStartup", "true");
        builder.UseSetting("ToolHosting:RegistryPath", ToolRegistryPath);
        builder.UseSetting("CampaignStorage:Path", Path.Combine(_directory, "campaign-access.json"));
        builder.UseSetting("Discord:WidgetUrl", "https://discord.com/widget?id=123456789&theme=dark");
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestRoleAuthenticationHandler.Scheme;
                options.DefaultChallengeScheme = TestRoleAuthenticationHandler.Scheme;
                options.DefaultForbidScheme = TestRoleAuthenticationHandler.Scheme;
            }).AddScheme<AuthenticationSchemeOptions, TestRoleAuthenticationHandler>(
                TestRoleAuthenticationHandler.Scheme,
                _ => { });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        if (Interlocked.Exchange(ref _seeded, 1) == 0)
        {
            using var scope = host.Services.CreateScope();
            var authoring = scope.ServiceProvider.GetRequiredService<IContentAuthoringService>();
            var assets = scope.ServiceProvider.GetRequiredService<IContentAssetService>();
            SeedAsync(authoring, assets).GetAwaiter().GetResult();
        }
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static async Task SeedAsync(IContentAuthoringService authoring, IContentAssetService assets)
    {
        await CreateAsync(authoring, new ContentItem
        {
            Id = "article-bees",
            Slug = "legacy-bees-article",
            Title = "Freeing the Bees: Solving ConsoleVariations",
            Summary = "A debugging story about freeing the bees.",
            DateText = "August 12, 2026",
            LinkText = "Read article",
            Tags = [ContentTags.Article, "software-development"],
            VisibleInModes = [BuiltInSiteModes.Professional.Id],
            Body = "## Freeing the Bees\n\n- Inspect the console\n- Fix the variation"
        });
        var article = await authoring.GetEditAsync("External", "legacy-bees-article");
        if (article is null)
        {
            throw new InvalidOperationException("The seeded article could not be reloaded.");
        }
        article.Document.Slug = "freeing-the-bees-consolevariations-puzzle";
        await authoring.SaveRevisionAsync(article.Document);

        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        await using var imageStream = new MemoryStream(png);
        var image = await assets.UploadAsync("External", "fixture-image.png", "image/png", imageStream, png.Length);
        await assets.AttachAsync(
            "External", "freeing-the-bees-consolevariations-puzzle", "External", image.AssetKey);
        article = await authoring.GetEditAsync("External", "freeing-the-bees-consolevariations-puzzle");
        if (article is null)
        {
            throw new InvalidOperationException("The seeded article could not be reloaded after media upload.");
        }
        article.Document.Body += $"\n\n![Fixture image]({image.Url})";
        await authoring.SaveRevisionAsync(article.Document);

        await CreateAsync(authoring, Project(
            "personal-multi-mode-website", "personalmultimodewebsite", "Personal Multi-Mode Website",
            "## Architecture Flow\n\nResolve Mode → Check Access → Fallback Piece\n\n{{site-mode-architecture}}", ["architecture", "web-development"],
            logoUrl: "/site-modes/professional/images/favicon.svg", logoAlt: "Kyle Barnett favicon"));
        await CreateAsync(authoring, Project(
            "dnd-tools", "dndtools", "D&D Tools", "## D&D Tools", ["web-development"],
            logoUrl: "/favicon.ico", logoAlt: "Dorks & Dice logo"));
        await CreateAsync(authoring, Project(
            "skyblivion", "experienceskyblivion", "Skyblivion", "## Skyblivion\n\nGallery presentation.", ["game-development"]));
        var skyblivion = await authoring.GetEditAsync("External", "experienceskyblivion");
        if (skyblivion is null)
        {
            throw new InvalidOperationException("The seeded Skyblivion page could not be reloaded.");
        }
        skyblivion.Document.Slug = "skyblivion";
        await authoring.SaveRevisionAsync(skyblivion.Document);
        await CreateAsync(authoring, Project(
            "python-finance-analytics", "pythonfinanceanalytics", "Python Finance Analytics",
            "[View Notebook on GitHub](https://github.com/LegendMaster03/python-finance-analytics/blob/main/finance-analysis.ipynb)\n\n[View Repository](https://github.com/LegendMaster03/python-finance-analytics)",
            ["python"], repositoryUrl: "https://github.com/LegendMaster03/python-finance-analytics"));
        await CreateAsync(authoring, Project("xngine", "xngine", "Xngine", "## Xngine", ["architecture"]));

        await CreateAsync(authoring, new ContentItem
        {
            Id = "experience-cybersecurity-team",
            Slug = "experiencecybersecurityteam",
            Title = "Cybersecurity Team",
            Summary = "Professional cybersecurity experience.",
            LinkText = "View experience",
            Tags = [ContentTags.Experience, "cybersecurity"],
            VisibleInModes = [BuiltInSiteModes.Professional.Id],
            Body = "## Experience Information"
        });

        var seniorProject = Project(
            "senior-project", "seniorproject", "Senior Project", "## Experience Information", ["web-development"]);
        seniorProject.Tags.Add(ContentTags.Experience);
        seniorProject.Presentations[ContentTags.Experience] = new ContentPresentation
        {
            Title = "Safe Future Foundation - Full-Stack Developer",
            Summary = "Full-stack development experience.",
            LinkText = "View experience"
        };
        await CreateAsync(authoring, seniorProject);
    }

    private static ContentItem Project(
        string id,
        string slug,
        string title,
        string body,
        IEnumerable<string> publicTags,
        string? repositoryUrl = null,
        string? logoUrl = null,
        string? logoAlt = null) => new()
        {
            Id = id,
            Slug = slug,
            Title = title,
            Summary = $"{title} project summary.",
            LinkText = "View project",
            Featured = true,
            RepositoryUrl = repositoryUrl,
            Header = new ContentDetailHeader
            {
                LogoUrl = logoUrl,
                LogoAltText = logoAlt,
                LogoLinkUrl = logoUrl is null ? null : "https://example.test"
            },
            Tags = [ContentTags.Project, .. publicTags],
            VisibleInModes = [BuiltInSiteModes.Professional.Id],
            Body = body
        };

    private static Task CreateAsync(IContentAuthoringService authoring, ContentItem item) =>
        authoring.CreateAsync(new ContentAuthoringDocument
        {
            IsNew = true,
            SourceKey = "External",
            Id = item.Id,
            Slug = item.Slug,
            IsListed = true,
            MetadataJson = JsonSerializer.Serialize(item),
            TagsText = string.Join('\n', item.Tags),
            VisibleModesSelection = item.VisibleInModes.ToList(),
            BodyFormat = "markdown",
            Body = item.Body
        });
}
