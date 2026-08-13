namespace dorks_and_dice_site.Services.Content;

public interface IContentBodyRenderer
{
    string Render(string format, string body);
}
