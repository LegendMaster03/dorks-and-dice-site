namespace dorks_and_dice_site.Models.Resume;

public class ProjectItemViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Category { get; set; } = "professional";
    public string Action { get; set; } = string.Empty;
    public string LinkText { get; set; } = "Open Project";
    public bool Featured { get; set; }
    public List<string> Tags { get; set; } = [];
}
