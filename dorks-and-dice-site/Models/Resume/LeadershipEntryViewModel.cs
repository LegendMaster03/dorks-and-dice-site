namespace dorks_and_dice_site.Models.Resume;

public class LeadershipEntryViewModel
{
    public string Title { get; set; } = string.Empty;
    public string DateRange { get; set; } = string.Empty;
    public string CardCssClass { get; set; } = "card mb-2";
    public string? RelatedProjectAction { get; set; }
    public string? RelatedProjectLabel { get; set; }
    public List<string> Highlights { get; set; } = [];
    public string? PrimaryAction { get; set; }
    public string PrimaryActionText { get; set; } = "Open Project";
}
