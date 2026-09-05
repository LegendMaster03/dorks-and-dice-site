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
/// Splits authored Markdown into ordered Markdown and installed-component fragments. Components
/// must occupy their own line and use quoted key/value parameters so authored content can select
/// installed capabilities without becoming executable HTML or code.
/// </summary>
public sealed class ContentPageComposer : IContentPageComposer
{
    private static readonly Regex ComponentPattern = new(
        @"^[\t ]*\{\{(?<name>[a-z0-9-]+)(?<arguments>(?:[\t ]+[a-z][a-z0-9-]*=\"[^\"\r\n]*\")+)[\t ]*\}\}[\t ]*(?:\r?\n|$)",
        RegexOptions.Compiled
        | RegexOptions.CultureInvariant
        | RegexOptions.IgnoreCase
        | RegexOptions.Multiline,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex ArgumentPattern = new(
        @"[\t ]+(?<key>[a-z][a-z0-9-]*)=\"(?<value>[^\"\r\n]*)\"",
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
        var fragments = new List<ContentPageFragment>();
        var currentIndex = 0;

        foreach (Match match in ComponentPattern.Matches(body))
        {
            if (match.Index > currentIndex)
            {
                AddMarkdownFragment(fragments, format, body[currentIndex..match.Index]);
            }

            var sourceName = match.Groups["name"].Value;
            if (!_definitions.TryGetValue(sourceName, out var definition))
            {
                throw new InvalidOperationException(
                    $"Content page component '{sourceName}' is not installed.");
            }

            var parameters = ParseParameters(sourceName, match.Groups["arguments"].Value);
            definition.Validate(parameters);
            fragments.Add(ContentPageFragment.ComponentInvocation(new ContentPageComponentInvocation
            {
                Name = definition.Name,
                ViewComponentName = definition.ViewComponentName,
                Parameters = parameters
            }));

            currentIndex = match.Index + match.Length;
        }

        if (currentIndex < body.Length)
        {
            AddMarkdownFragment(fragments, format, body[currentIndex..]);
        }

        if (fragments.Count == 0)
        {
            AddMarkdownFragment(fragments, format, body);
        }

        return fragments;
    }

    private void AddMarkdownFragment(
        ICollection<ContentPageFragment> fragments,
        string format,
        string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return;
        }

        fragments.Add(ContentPageFragment.Html(_bodyRenderer.Render(format, markdown)));
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
