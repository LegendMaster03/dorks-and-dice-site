using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;

namespace dorks_and_dice_site.Tests;

public sealed class ContentPageComposerTests
{
    [Fact]
    public void ParameterizedCollectionSplitsRenderedDocumentInOrder()
    {
        var composer = CreateComposer();
        var body = """
            ## Experience

            Intro copy.

            {{content-collection context="experience" presentation="professional-experience" order="first,second"}}

            ## Education
            """;

        var fragments = composer.Compose("markdown", body);

        Assert.Equal(3, fragments.Count);
        Assert.Contains("Experience", fragments[0].RenderedHtml, StringComparison.Ordinal);
        Assert.NotNull(fragments[1].Component);
        Assert.Equal("content-collection", fragments[1].Component!.Name);
        Assert.Equal("ContentCollection", fragments[1].Component.ViewComponentName);
        Assert.Equal("experience", fragments[1].Component.Parameters["context"]);
        Assert.Equal("professional-experience", fragments[1].Component.Parameters["presentation"]);
        Assert.Equal("first,second", fragments[1].Component.Parameters["order"]);
        Assert.Contains("Education", fragments[2].RenderedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void ComponentInsideCustomContainerPreservesContainerAcrossFragments()
    {
        var composer = CreateComposer();
        var body = """
            ::: {.card .p-3}
            Before component.

            {{content-collection context="experience" presentation="professional-experience"}}

            After component.
            :::
            """;

        var fragments = composer.Compose("markdown", body);

        Assert.Equal(3, fragments.Count);
        Assert.Contains("class=\"card p-3\"", fragments[0].RenderedHtml, StringComparison.Ordinal);
        Assert.Contains("Before component.", fragments[0].RenderedHtml, StringComparison.Ordinal);
        Assert.NotNull(fragments[1].Component);
        Assert.Contains("After component.", fragments[2].RenderedHtml, StringComparison.Ordinal);
        Assert.Contains("</div>", fragments[2].RenderedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void DescendingFenceLengthsPreserveNestedGridContainers()
    {
        var composer = CreateComposer();
        var body = """
            ::::: {.test-section}
            :::: {.row}
            ::: {.col-md-6}
            Left
            :::
            ::: {.col-md-6}
            Right
            :::
            ::::
            :::::
            """;

        var fragment = Assert.Single(composer.Compose("markdown", body));
        var html = fragment.RenderedHtml!;

        Assert.Contains("class=\"test-section\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"row\"", html, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(html, "class=\"col-md-6\""));
        Assert.Contains("Left", html, StringComparison.Ordinal);
        Assert.Contains("Right", html, StringComparison.Ordinal);
    }

    [Fact]
    public void InstalledParameterlessPageComponentBecomesInvocation()
    {
        var composer = new ContentPageComposer(
            new ContentBodyRenderer(Array.Empty<IContentDirectiveRenderer>()),
            [
                new ContentCollectionPageComponentDefinition(),
                new TestPageComponentDefinition("discord-widget", "DiscordWidget")
            ]);

        var fragments = composer.Compose("markdown", "Before\n\n{{discord-widget}}\n\nAfter");

        Assert.Equal(3, fragments.Count);
        Assert.Equal("discord-widget", fragments[1].Component?.Name);
        Assert.Equal("DiscordWidget", fragments[1].Component?.ViewComponentName);
        Assert.Empty(fragments[1].Component!.Parameters);
    }

    [Fact]
    public void UnknownParameterizedComponentIsRejected()
    {
        var composer = CreateComposer();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            composer.Compose("markdown", "{{not-installed value=\"test\"}}"));

        Assert.Contains("not installed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CollectionRejectsUnsupportedParameters()
    {
        var composer = CreateComposer();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            composer.Compose(
                "markdown",
                "{{content-collection context=\"experience\" presentation=\"professional-experience\" unsafe=\"true\"}}"));

        Assert.Contains("does not support parameter 'unsafe'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingParameterlessDirectivesRemainMarkdownRendererResponsibility()
    {
        var renderer = new ContentBodyRenderer(
        [
            new StaticContentDirectiveRenderer("content-note-start", "<aside class=\"content-note\">"),
            new StaticContentDirectiveRenderer("content-note-end", "</aside>")
        ]);
        var composer = new ContentPageComposer(
            renderer,
            [new ContentCollectionPageComponentDefinition()]);

        var fragments = composer.Compose("markdown", "{{content-note-start}}\nText\n{{content-note-end}}");

        var fragment = Assert.Single(fragments);
        Assert.Contains("content-note", fragment.RenderedHtml, StringComparison.Ordinal);
        Assert.Contains("Text", fragment.RenderedHtml, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    private static ContentPageComposer CreateComposer() => new(
        new ContentBodyRenderer(Array.Empty<IContentDirectiveRenderer>()),
        [new ContentCollectionPageComponentDefinition()]);

    private sealed class TestPageComponentDefinition(string name, string viewComponentName)
        : IContentPageComponentDefinition
    {
        public string Name { get; } = name;
        public string ViewComponentName { get; } = viewComponentName;

        public void Validate(IReadOnlyDictionary<string, string> parameters)
        {
            if (parameters.Count != 0)
            {
                throw new InvalidOperationException("No parameters are supported.");
            }
        }
    }
}
