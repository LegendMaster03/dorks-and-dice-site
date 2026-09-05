namespace dorks_and_dice_site.Models.Content;

/// <summary>
/// One ordered fragment of a composed database-backed page. A fragment is either rendered
/// authored Markdown or an invocation of an installed application-owned page component.
/// </summary>
public sealed class ContentPageFragment
{
    public string? RenderedHtml { get; init; }
    public ContentPageComponentInvocation? Component { get; init; }

    public static ContentPageFragment Html(string renderedHtml) => new()
    {
        RenderedHtml = renderedHtml
    };

    public static ContentPageFragment ComponentInvocation(ContentPageComponentInvocation component) => new()
    {
        Component = component
    };
}

/// <summary>
/// Validated component invocation parsed from authored content. The source name remains the
/// stable authoring contract while ViewComponentName identifies the installed executable
/// capability that renders it.
/// </summary>
public sealed class ContentPageComponentInvocation
{
    public required string Name { get; init; }
    public required string ViewComponentName { get; init; }
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }

    public string GetRequiredParameter(string name)
    {
        if (!Parameters.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Content component '{Name}' requires parameter '{name}'.");
        }

        return value;
    }

    public string? GetOptionalParameter(string name) =>
        Parameters.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
}
