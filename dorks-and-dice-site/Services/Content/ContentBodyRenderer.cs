using System.Text;
using System.Text.RegularExpressions;
using Ganss.Xss;
using Markdig;

namespace dorks_and_dice_site.Services.Content;

public sealed class ContentBodyRenderer : IContentBodyRenderer
{
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    // Directives are application-owned block elements. Requiring them to occupy their own line prevents
    // directive expansion from changing the meaning of surrounding user-authored Markdown.
    private static readonly Regex DirectivePattern = new(
        @"^[\t ]*\{\{(?<name>[a-z0-9-]+)\}\}[\t ]*(?:\r?\n|$)",
        RegexOptions.Compiled
        | RegexOptions.CultureInvariant
        | RegexOptions.IgnoreCase
        | RegexOptions.Multiline);

    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    private readonly IReadOnlyDictionary<string, IContentDirectiveRenderer> _directives;

    public ContentBodyRenderer(IEnumerable<IContentDirectiveRenderer> directives)
    {
        _directives = directives.ToDictionary(
            directive => directive.Name,
            StringComparer.OrdinalIgnoreCase);
    }

    public string Render(string format, string body)
    {
        if (!string.Equals(format, "markdown", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Content body format '{format}' is not supported.");
        }

        if (body.Length > ContentInputPolicy.MaxBodyLength)
        {
            throw new InvalidOperationException(
                $"Content body exceeds the maximum length of {ContentInputPolicy.MaxBodyLength:N0} characters.");
        }

        var rendered = RenderMarkdownAndDirectives(body);
        return Sanitizer.Sanitize(rendered);
    }

    private string RenderMarkdownAndDirectives(string body)
    {
        var html = new StringBuilder();
        var currentIndex = 0;

        foreach (Match match in DirectivePattern.Matches(body))
        {
            if (match.Index > currentIndex)
            {
                html.Append(Markdown.ToHtml(body[currentIndex..match.Index], MarkdownPipeline));
            }

            var name = match.Groups["name"].Value;
            if (!_directives.TryGetValue(name, out var directive))
            {
                throw new InvalidOperationException($"Content directive '{name}' is not registered.");
            }

            // Directive renderers are application code rather than authored content. Their output still passes
            // through the final sanitizer below so a future renderer does not become an accidental XSS bypass.
            html.Append(directive.Render());
            currentIndex = match.Index + match.Length;
        }

        if (currentIndex < body.Length)
        {
            html.Append(Markdown.ToHtml(body[currentIndex..], MarkdownPipeline));
        }

        return html.ToString();
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();

        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[]
        {
            "a", "aside", "blockquote", "br", "code", "del", "details",
            "div", "em", "figcaption", "figure", "h1", "h2", "h3",
            "h4", "h5", "h6", "hr", "img", "li", "ol", "p", "pre",
            "span", "strong", "summary", "table", "tbody", "td", "th",
            "thead", "tr", "ul"
        })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        foreach (var attribute in new[]
        {
            "alt", "class", "colspan", "download", "height", "href", "id", "rel",
            "role", "rowspan", "scope", "src", "target", "title", "width"
        })
        {
            sanitizer.AllowedAttributes.Add(attribute);
        }

        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("mailto");
        sanitizer.AllowedSchemes.Add("tel");

        return sanitizer;
    }
}
