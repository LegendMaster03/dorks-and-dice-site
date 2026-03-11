namespace dorks_and_dice_site.Models.Resume;

public class ResumeDetailHeaderViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? MetaLine { get; set; }
    public string? LogoUrl { get; set; }
    public string? LogoAltText { get; set; }
    public string? LogoLinkUrl { get; set; }
    public string? LogoAriaLabel { get; set; }
    public Dictionary<string, string> InfoItems { get; set; } = [];
}
