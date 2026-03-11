namespace dorks_and_dice_site.Models.Resume;

public class ResumeHeaderViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string HeadshotImageUrl { get; set; } = string.Empty;
    public string HeadshotAltText { get; set; } = string.Empty;
    public string ResumePdfUrl { get; set; } = string.Empty;
    public string ResumePdfFileName { get; set; } = string.Empty;
    public string LastUpdatedText { get; set; } = string.Empty;
}
