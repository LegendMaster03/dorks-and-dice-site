using dorks_and_dice_site.Services.Content;

namespace dorks_and_dice_site.Tests;

public sealed class RichMarkdownLayoutTests
{
    [Fact]
    public void RendererPreservesSafeContainerAndElementClasses()
    {
        var renderer = new ContentBodyRenderer(Array.Empty<IContentDirectiveRenderer>());

        var html = renderer.Render(
            "markdown",
            """
            ::: {.card .shadow-sm}
            ## Card title {.h4}

            [Open page](/articles){.btn .btn-primary}
            :::
            """);

        Assert.Contains("card shadow-sm", html, StringComparison.Ordinal);
        Assert.Contains("class=\"h4\"", html, StringComparison.Ordinal);
        Assert.Contains("btn btn-primary", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/articles\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RendererStillStripsUnapprovedGenericAttributes()
    {
        var renderer = new ContentBodyRenderer(Array.Empty<IContentDirectiveRenderer>());

        var html = renderer.Render(
            "markdown",
            """
            ::: {.card #page-shell onclick="alert(1)"}
            Safe content.
            :::
            """);

        Assert.Contains("class=\"card\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", html, StringComparison.OrdinalIgnoreCase);
    }
}
