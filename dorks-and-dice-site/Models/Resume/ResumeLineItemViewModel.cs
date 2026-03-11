namespace dorks_and_dice_site.Models.Resume;

public class ResumeLineItemViewModel
{
    public string Text { get; set; } = string.Empty;
    public string CssClass { get; set; } = "mb-0";
    public bool IsMuted { get; set; }
}
