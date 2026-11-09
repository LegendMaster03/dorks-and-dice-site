namespace dorks_and_dice_site.Services.Content;

/// <summary>
/// Converts an authored body to sanitized HTML without choosing the active
/// site's layout or mode-owned view components.
/// </summary>
public interface IContentBodyRenderer
{
    string Render(string format, string body, string? sameApplicationHost = null);
}
