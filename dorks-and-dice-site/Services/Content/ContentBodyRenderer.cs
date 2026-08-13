using System.Text.RegularExpressions;
using Markdig;

namespace dorks_and_dice_site.Services.Content;

public sealed class ContentBodyRenderer : IContentBodyRenderer
{
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private static readonly Regex DirectivePattern = new(
        @"\{\{(?<name>[a-z0-9-]+)\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly IReadOnlyDictionary<string, IContentDirectiveRenderer> _directives;

    public ContentBodyRenderer(IEnumerable<IContentDirectiveRenderer> directives)
    {
        _directives = directives.ToDictionary(
            directive => directive.Name,
            StringComparer.OrdinalIgnoreCase);
    }

    public string Render(string format, string body)
    {
        var expandedBody = DirectivePattern.Replace(body, match =>
        {
            var name = match.Groups["name"].Value;
            if (!_directives.TryGetValue(name, out var directive))
            {
                throw new InvalidOperationException($"Content directive '{name}' is not registered.");
            }

            return directive.Render();
        });

        return format.ToLowerInvariant() switch
        {
            "markdown" => Markdown.ToHtml(expandedBody, MarkdownPipeline),
            _ => throw new NotSupportedException($"Content body format '{format}' is not supported.")
        };
    }
}
