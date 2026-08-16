using dorks_and_dice_site.Services.Content;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dorks_and_dice_site.Tests;

[Collection(PublishedContentIntegrationCollection.Name)]
public sealed class InitialContentMarkdownIntegrationTests
{
    private readonly PublishedContentWebApplicationFactory _factory;

    public InitialContentMarkdownIntegrationTests(PublishedContentWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public void RepresentativeMigratedMarkdownRendersAsStructuredSafeHtml()
    {
        var renderer = new ContentBodyRenderer(Array.Empty<IContentDirectiveRenderer>());
        var html = renderer.Render("markdown", """
            ## Heading

            A paragraph with **bold**, *italic*, and a [link](/resume).

            - First
            - Second

            > **Warning:** Read this.

            | Name | Value |
            | --- | --- |
            | One | Two |

            ![Example](/content/media/0123456789abcdef0123456789abcdef/example.png)
            """);

        Assert.Contains("<h2", html);
        Assert.Contains("<p>", html);
        Assert.Contains("<ul>", html);
        Assert.Contains("<blockquote>", html);
        Assert.Contains("<table>", html);
        Assert.Contains("src=\"/content/media/0123456789abcdef0123456789abcdef/example.png\"", html);
        Assert.DoesNotContain("&lt;h2", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("&lt;p", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MarkdownContentPresentationCssRemainsResponsive()
    {
        var css = await _factory.CreateClient().GetStringAsync("/css/site.css");
        Assert.Matches(@"\.content-detail-body\s+img\s*\{[^}]*max-width:\s*100%;[^}]*height:\s*auto;", css);
        Assert.Matches(@"\.content-detail-body\s+table\s*\{[^}]*width:\s*100%;", css);
    }
}
