namespace dorks_and_dice_site.Services.Content;

public sealed class StaticContentDirectiveRenderer : IContentDirectiveRenderer
{
    public StaticContentDirectiveRenderer(string name, string html)
    {
        Name = name;
        _html = html;
    }

    private readonly string _html;

    public string Name { get; }

    public string Render() => _html;
}
