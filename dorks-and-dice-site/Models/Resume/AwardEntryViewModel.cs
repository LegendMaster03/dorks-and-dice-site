namespace dorks_and_dice_site.Models.Resume;

public class AwardEntryViewModel
{
    public string Title { get; set; } = string.Empty;
    public string CardCssClass { get; set; } = "card mb-2";
    public string? MetaText { get; set; }
    public string? Summary { get; set; }
    public List<string> Highlights { get; set; } = [];
    public string? AdditionalDescription { get; set; }
    // Optional embedded credential PDF (relative URL, e.g. "~/files/award.pdf")
    public string? EmbedUrl { get; set; }
    // Local backup file (relative URL)
    public string? LocalBackupUrl { get; set; }
}
