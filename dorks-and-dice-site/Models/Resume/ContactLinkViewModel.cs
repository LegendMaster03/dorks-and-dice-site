namespace dorks_and_dice_site.Models.Resume;

public class ContactLinkViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Href { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public string IconAltText { get; set; } = string.Empty;
    public string? IconCssClass { get; set; }
    public bool OpenInNewTab { get; set; }
}
