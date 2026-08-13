using Markdig;

namespace dorks_and_dice_site.Services.Content;

public sealed class ContentBodyRenderer : IContentBodyRenderer
{
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public string Render(string format, string body)
    {
        return format.ToLowerInvariant() switch
        {
            "markdown" => Markdown.ToHtml(body, MarkdownPipeline),
            _ => throw new NotSupportedException($"Content body format '{format}' is not supported.")
        };
    }
}
