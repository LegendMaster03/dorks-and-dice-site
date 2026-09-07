using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

/// <summary>
/// Contract for the Professional portfolio's public architecture-summary content component.
/// It remains in the Services.Site namespace temporarily so existing callers do not need a
/// compatibility shim while the physical ownership is corrected.
/// </summary>
public interface ISiteModeArchitectureSummaryService
{
    SiteModeArchitectureSummaryViewModel GetSummary();
}
