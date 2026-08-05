namespace dorks_and_dice_site.Models.Articles;

public class ArticleItemViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Controller { get; set; } = "Articles";
    public string Action { get; set; } = string.Empty;
    public string PostedDateText { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string ImageAltText { get; set; } = string.Empty;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public bool Listed { get; set; } = true;
    public bool Professional { get; set; }
    public List<string> Tags { get; set; } = [];
}
