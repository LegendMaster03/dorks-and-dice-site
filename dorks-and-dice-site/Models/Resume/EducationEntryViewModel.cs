namespace dorks_and_dice_site.Models.Resume;

public class EducationEntryViewModel
{
    public string Institution { get; set; } = string.Empty;
    public string CardCssClass { get; set; } = "card mb-2";
    public List<ResumeLineItemViewModel> Lines { get; set; } = [];
}
