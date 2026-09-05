using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content;

namespace dorks_and_dice_site.Tests;

public sealed class ContentPageComposerTests
{
    [Fact]
    public void ParameterizedCollectionSplitsMarkdownInOrder()
    {
        var composer = new ContentPageComposer(
            new TestBodyRenderer(),
            [new ContentCollectionPageComponentDefinition()]);
        var body = """
            ## Experience

            Intro copy.

            {{content-collection context="experience" presentation="professional-experience" order="first,second"}}

            ## Education
            """;

        var fragments = composer.Compose("markdown", body);

        Assert.Equal(3, fragments.Count);
        Assert.Contains("## Experience", fragments[0].RenderedHtml, StringComparison.Ordinal);
        Assert.NotNull(fragments[1].Component);
        Assert.Equal("content-collection", fragments[1].Component!.Name);
        Assert.Equal("ContentCollection", fragments[1].Component.ViewComponentName);
        Assert.Equal("experience", fragments[1].Component.Parameters["context"]);
        Assert.Equal("professional-experience", fragments[1].Component.Parameters["presentation"]);
        Assert.Equal("first,second", fragments[1].Component.Parameters["order"]);
        Assert.Contains("## Education", fragments[2].RenderedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownParameterizedComponentIsRejected()
    {
        var composer = new ContentPageComposer(
            new TestBodyRenderer(),
            [new ContentCollectionPageComponentDefinition()]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            composer.Compose("markdown", "{{not-installed value=\"test\"}}"));

        Assert.Contains("not installed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CollectionRejectsUnsupportedParameters()
    {
        var composer = new ContentPageComposer(
            new TestBodyRenderer(),
            [new ContentCollectionPageComponentDefinition()]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            composer.Compose(
                "markdown",
                "{{content-collection context=\"experience\" presentation=\"professional-experience\" unsafe=\"true\"}}"));

        Assert.Contains("does not support parameter 'unsafe'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingParameterlessDirectivesRemainMarkdownRendererResponsibility()
    {
        var renderer = new TestBodyRenderer();
        var composer = new ContentPageComposer(
            renderer,
            [new ContentCollectionPageComponentDefinition()]);

        var fragments = composer.Compose("markdown", "{{content-note-start}}\nText\n{{content-note-end}}");

        var fragment = Assert.Single(fragments);
        Assert.Contains("{{content-note-start}}", fragment.RenderedHtml, StringComparison.Ordinal);
        Assert.Equal(1, renderer.CallCount);
    }

    private sealed class TestBodyRenderer : IContentBodyRenderer
    {
        public int CallCount { get; private set; }

        public string Render(string format, string body)
        {
            CallCount++;
            return $"rendered:{format}:{body}";
        }
    }
}
