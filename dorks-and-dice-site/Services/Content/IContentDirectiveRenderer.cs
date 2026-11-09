namespace dorks_and_dice_site.Services.Content;

/// <summary>
/// Installed parameterless directive with trusted application-supplied HTML.
/// Authoring may invoke a registered name but can not supply arbitrary markup.
/// </summary>
public interface IContentDirectiveRenderer
{
    string Name { get; }
    string Render();
}
