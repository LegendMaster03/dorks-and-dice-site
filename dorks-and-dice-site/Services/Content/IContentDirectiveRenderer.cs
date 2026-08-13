namespace dorks_and_dice_site.Services.Content;

public interface IContentDirectiveRenderer
{
    string Name { get; }
    string Render();
}
