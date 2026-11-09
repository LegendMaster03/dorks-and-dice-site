namespace dorks_and_dice_site.Models.Site;

/// <summary>
/// Identifies presentation metadata requested from a mode module; missing
/// parts can be handled by the framework's registered fallback presentation.
/// </summary>
public enum SiteModePresentationPart
{
    TitleSuffix,
    DefaultMetaDescription,
    ArticlesIndex,
    Favicon,
    DefaultMetaImage,
    StructuredData
}
