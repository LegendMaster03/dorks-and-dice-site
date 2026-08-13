using Markdig;

namespace dorks_and_dice_site.Services.Content;

public sealed class MarkdownContentBodyRenderer : IContentBodyRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public string RenderMarkdown(string markdown) => Markdown.ToHtml(markdown, Pipeline);
}
