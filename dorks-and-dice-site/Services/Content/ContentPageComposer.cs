using System.Text.RegularExpressions;
using dorks_and_dice_site.Models.Content;

namespace dorks_and_dice_site.Services.Content;

public interface IContentPageComponentDefinition
{
    string Name { get; }
    string ViewComponentName { get; }
    void Validate(IReadOnlyDictionary<string, string> parameters);
}

public interface IContentPageComposer
{
    IReadOnlyList<ContentPageFragment> Compose(string format, string body);
}

/// <summary>
/// Parses application-owned page components from authored Markdown while rendering the Markdown
/// document only once. Components are replaced with inert paragraph markers before Markdown
/// rendering, then the sanitized HTML is split back around those markers. This preserves custom
/// containers, grids, cards, and other Markdown structure that surrounds a component.
/// Parameterless lines remain available to the existing directive renderer unless an installed
/// page-component definition claims the name.
/// </summary>
public sealed class ContentPageComposer : IContentPageComposer
{
    private static readonly Regex ComponentPattern = new(
        "^[\\t ]*\\{\\{(?<name>[a-z0-9-]+)(?<arguments>(?:[\\t ]+[a-z][a-z0-9-]*=\"[^\"\\r\\n]*\")*)[\\t ]*\\}\\}[\\t ]*(?:\\r?\\n|$)",
        RegexOptions.Compiled
        | RegexOptions.CultureInvariant
        | RegexOptions.IgnoreCase
        | RegexOptions.Multiline,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex ArgumentPattern = new(
        "[\\t ]+(?<key>[a-z][a-z0-9-]*)=\"(?<value>[^\"\\r\\n]*)\"",
        RegexOptions.Compiled
        | RegexOptions.CultureInvariant
        | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(100));

    private readonly IContentBodyRenderer _bodyRenderer;
    private readonly IReadOnlyDictionary<string, IContentPageComponentDefinition> _definitions;

    public ContentPageComposer(
        IContentBodyRenderer bodyRenderer,
        IEnumerable<IContentPageComponentDefinition> definitions)
    {
        _bodyRenderer = bodyRenderer;
        _definitions = definitions.ToDictionary(definition => definition.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ContentPageFragment> Compose(string format, string body)
    {
        var invocations = new List<ContentPageComponentInvocation>();
        var markerPrefix = $"CONTENTPAGECOMPONENT{Guid.NewGuid():N}";

        var protectedMarkdown = ComponentPattern.Replace(body, match =>
        {
            var sourceName = match.Groups["name"].Value;
            var arguments = match.Groups["arguments"].Value;
            if (!_definitions.TryGetValue(sourceName, out var definition))
            {
                // Parameterless application directives are an older, still-supported Markdown
                // extension point and remain the body renderer's responsibility.
                if (string.IsNullOrWhiteSpace(arguments))
                {
                    return match.Value;
                }

                throw new InvalidOperationException(
                    $"Content page component '{sourceName}' is not installed.");
            }

            var parameters = ParseParameters(sourceName, arguments);
            definition.Validate(parameters);
            var index = invocations.Count;
            invocations.Add(new ContentPageComponentInvocation
            {
                Name = definition.Name,
                ViewComponentName = definition.ViewComponentName,
                Parameters = parameters
            });

            return $"{markerPrefix}{index}END{Environment.NewLine}";
        });

        var renderedHtml = _bodyRenderer.Render(format, protectedMarkdown);
        if (invocations.Count == 0)
        {
            return [ContentPageFragment.Html(renderedHtml)];
        }

        var fragments = new List<ContentPageFragment>();
        var currentIndex = 0;
        for (var index = 0; index < invocations.Count; index++)
        {
            var markerHtml = $"<p>{markerPrefix}{index}END</p>";
            var markerIndex = renderedHtml.IndexOf(markerHtml, currentIndex, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                throw new InvalidOperationException(
                    "Rendered page component marker could not be resolved. The component must occupy its own Markdown line.");
            }

            if (markerIndex > currentIndex)
            {
                fragments.Add(ContentPageFragment.Html(renderedHtml[currentIndex..markerIndex]));
            }

            fragments.Add(ContentPageFragment.ComponentInvocation(invocations[index]));
            currentIndex = markerIndex + markerHtml.Length;
        }

        if (currentIndex < renderedHtml.Length)
        {
            fragments.Add(ContentPageFragment.Html(renderedHtml[currentIndex..]));
        }

        return fragments;
    }

    private static IReadOnlyDictionary<string, string> ParseParameters(string componentName, string arguments)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match argument in ArgumentPattern.Matches(arguments))
        {
            var key = argument.Groups["key"].Value;
            if (!parameters.TryAdd(key, argument.Groups["value"].Value))
            {
                throw new InvalidOperationException(
                    $"Content page component '{componentName}' contains duplicate parameter '{key}'.");
            }
        }

        return parameters;
    }
}
