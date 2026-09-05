using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Site;

namespace dorks_and_dice_site.Services.Content;

public interface IHomepageContentService
{
    Task<HomepageContentViewModel?> GetAsync(
        SiteModeContext modeContext,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves the single database-backed homepage document visible to a normal mode and composes
/// its authored Markdown with installed page components. Mode definition storage is deliberately
/// outside this service so the same page contract works whether normal modes ultimately remain
/// compiled definitions or become runtime data.
/// </summary>
public sealed class HomepageContentService : IHomepageContentService
{
    private readonly IContentCatalogService _catalog;
    private readonly IContentPageComposer _pageComposer;

    public HomepageContentService(
        IContentCatalogService catalog,
        IContentPageComposer pageComposer)
    {
        _catalog = catalog;
        _pageComposer = pageComposer;
    }

    public async Task<HomepageContentViewModel?> GetAsync(
        SiteModeContext modeContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(modeContext);

        if (modeContext.ActiveModeId is null)
        {
            return null;
        }

        var candidates = await _catalog.GetByContextAsync(
            ContentTags.Homepage,
            modeContext,
            includeUnlisted: true,
            cancellationToken);

        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count > 1)
        {
            throw new InvalidOperationException(
                $"Site mode '{modeContext.ActiveModeId}' has multiple visible homepage documents. Exactly one homepage document may be active per mode.");
        }

        var item = candidates[0];
        return new HomepageContentViewModel
        {
            Item = item,
            Fragments = _pageComposer.Compose(item.BodyFormat, item.Body)
        };
    }
}
