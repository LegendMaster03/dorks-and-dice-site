using System.Text.Json.Nodes;
using dorks_and_dice_site.Services.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
using Microsoft.Extensions.Configuration;

namespace dorks_and_dice_site.Tests;

public sealed class ContentSecurityTests
{
    [Fact]
    public void RendererDoesNotAllowAuthoredHtmlOrUnsafeSchemes()
    {
        var renderer = new ContentBodyRenderer(Array.Empty<IContentDirectiveRenderer>());

        var html = renderer.Render(
            "markdown",
            "**safe**\n\n<script>alert(1)</script>\n\n[bad](javascript:alert(1))\n\n<img src=x onerror=alert(1)>");

        Assert.Contains("<strong>safe</strong>", html);
        Assert.False(html.Contains("<script", StringComparison.OrdinalIgnoreCase));
        Assert.False(html.Contains("<img", StringComparison.OrdinalIgnoreCase));
        Assert.False(html.Contains("javascript:", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RendererAllowsTelephoneLinks()
    {
        var renderer = new ContentBodyRenderer(Array.Empty<IContentDirectiveRenderer>());

        var html = renderer.Render("markdown", "[Call](tel:+19048038980)");

        Assert.Contains("href=\"tel:+19048038980\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RendererPreservesAuthoredSectionIds()
    {
        var renderer = new ContentBodyRenderer(Array.Empty<IContentDirectiveRenderer>());

        var html = renderer.Render("markdown", "## Experience {#experience-section}");

        Assert.Contains("id=\"experience-section\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RendererPreservesSafeAuthoredPresentationClasses()
    {
        var renderer = new ContentBodyRenderer(Array.Empty<IContentDirectiveRenderer>());

        var html = renderer.Render(
            "markdown",
            "## Experience {#experience-section .resume-section-title}\n\n"
            + "![Portrait](/portrait.jpg){.profile-headshot .rounded-circle}\n\n"
            + "[Download](/resume.pdf){.btn .btn-primary download=resume.pdf}");

        Assert.Contains("class=\"resume-section-title\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("class=\"profile-headshot rounded-circle\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("class=\"btn btn-primary\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("download=\"resume.pdf\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RendererRejectsUnknownApplicationDirective()
    {
        var renderer = new ContentBodyRenderer(Array.Empty<IContentDirectiveRenderer>());

        Assert.Throws<InvalidOperationException>(() =>
            renderer.Render("markdown", "{{not-registered}}\n"));
    }

    [Fact]
    public async Task AuthoringRejectsReservedInternalTags()
    {
        using var fixture = new AuthoringFixture();
        var service = CreateAuthoringService(fixture.Registry);
        var model = CreateValidModel(service);
        model.Document.TagsText = "article\n_internal:unlisted";

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(model.Document));
    }

    [Fact]
    public async Task AuthoringRejectsUnknownMetadataProperties()
    {
        using var fixture = new AuthoringFixture();
        var service = CreateAuthoringService(fixture.Registry);
        var model = CreateValidModel(service);
        var metadata = JsonNode.Parse(model.Document.MetadataJson)!.AsObject();
        metadata["unexpected"] = "value";
        model.Document.MetadataJson = metadata.ToJsonString();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(model.Document));
    }

    [Fact]
    public async Task AuthoringRejectsUnsafeMetadataUrls()
    {
        using var fixture = new AuthoringFixture();
        var service = CreateAuthoringService(fixture.Registry);
        var model = CreateValidModel(service);
        var metadata = JsonNode.Parse(model.Document.MetadataJson)!.AsObject();
        metadata["repositoryUrl"] = "javascript:alert(1)";
        model.Document.MetadataJson = metadata.ToJsonString();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(model.Document));
    }

    [Fact]
    public async Task AuthoringRejectsMalformedRouteSlugBeforeDatabaseLookup()
    {
        using var fixture = new AuthoringFixture();
        var service = CreateAuthoringService(fixture.Registry);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetEditAsync("Test", "../bad"));
    }

    private static ContentAuthoringService CreateAuthoringService(IContentSourceRegistry sourceRegistry) =>
        new(sourceRegistry, new SiteModeRegistry(BuiltInSiteModes.All));

    private static dorks_and_dice_site.Models.Content.ContentAuthoringEditViewModel CreateValidModel(
        ContentAuthoringService service)
    {
        var model = service.GetNew("Test");
        model.Document.Id = "security-test";
        model.Document.Slug = "security-test";
        return model;
    }

    private sealed class AuthoringFixture : IDisposable
    {
        private readonly string _directory;

        public AuthoringFixture()
        {
            _directory = Path.Combine(Path.GetTempPath(), $"content-security-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:TestDb"] = "Data Source=test-content.db",
                ["ContentStorage:AuthoringSource"] = "Test",
                ["ContentStorage:Sources:Test:Provider"] = "Sqlite",
                ["ContentStorage:Sources:Test:ConnectionString"] = "TestDb"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
            Registry = new ContentSourceRegistry(configuration, _directory);
        }

        public ContentSourceRegistry Registry { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, recursive: true);
                }
            }
            catch (IOException)
            {
                // SQLite can briefly hold a file handle on Windows after a context is disposed.
            }
        }
    }
}
