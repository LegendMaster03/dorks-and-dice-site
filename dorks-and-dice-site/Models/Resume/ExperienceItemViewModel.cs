namespace dorks_and_dice_site.Models.Resume;

public class ExperienceItemViewModel
{
    public string Title { get; set; } = string.Empty;
    public string DateRange { get; set; } = string.Empty;
    public string CardCssClass { get; set; } = "card mb-2";
    public List<string> Highlights { get; set; } = [];
    public string? DetailsAction { get; set; }
    public string DetailsLinkText { get; set; } = "Detailed experience view";
    public bool Featured { get; set; }
    public List<string> Tags { get; set; } = [];
}
