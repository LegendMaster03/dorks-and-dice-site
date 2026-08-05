using dorks_and_dice_site.Models.Articles;
using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public sealed class SiteModePresentationService : ISiteModePresentationService
{
    private readonly IReadOnlyDictionary<SiteMode, ISiteModePresentationModule> _modules;
    private readonly ISiteModePresentationModule _unassignedModule;

    public SiteModePresentationService(IEnumerable<ISiteModePresentationModule> modules)
    {
        _modules = modules.ToDictionary(module => module.SiteMode);
        _unassignedModule = _modules[SiteMode.Unassigned];
    }

    public string GetTitleSuffix(SiteMode siteMode)
    {
        return Resolve(siteMode, SiteModePresentationPart.TitleSuffix, module => module.GetTitleSuffix());
    }

    public string GetDefaultMetaDescription(SiteMode siteMode)
    {
        return Resolve(siteMode, SiteModePresentationPart.DefaultMetaDescription, module => module.GetDefaultMetaDescription());
    }

    public ArticlesIndexPresentationViewModel GetArticlesIndexPresentation(SiteMode siteMode)
    {
        return Resolve(siteMode, SiteModePresentationPart.ArticlesIndex, module => module.GetArticlesIndexPresentation());
    }

    private T Resolve<T>(SiteMode siteMode, SiteModePresentationPart presentationPart, Func<ISiteModePresentationModule, T> resolve)
    {
        var module = _modules.GetValueOrDefault(siteMode) ?? _unassignedModule;
        try
        {
            return resolve(module);
        }
        catch (SiteModePresentationPartUnavailableException exception) when (exception.PresentationPart == presentationPart && module.SiteMode != SiteMode.Unassigned)
        {
            return resolve(_unassignedModule);
        }
    }
}
