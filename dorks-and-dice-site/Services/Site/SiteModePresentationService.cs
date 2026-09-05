using dorks_and_dice_site.Models.Articles;
using dorks_and_dice_site.Models.Site;

namespace dorks_and_dice_site.Services.Site;

public sealed class SiteModePresentationService : ISiteModePresentationService
{
    private readonly IReadOnlyDictionary<string, ISiteModePresentationModule> _modules;
    private readonly ISiteModePresentationModule _fallbackModule;
    private readonly IWebHostEnvironment _environment;

    public SiteModePresentationService(
        IEnumerable<ISiteModePresentationModule> modules,
        IWebHostEnvironment environment)
    {
        var materialized = modules.ToArray();
        var duplicateKey = materialized
            .GroupBy(module => module.PresentationKey, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateKey is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate presentation key '{duplicateKey.Key}'.");
        }

        _modules = materialized.ToDictionary(
            module => module.PresentationKey,
            StringComparer.Ordinal);
        _fallbackModule = _modules.TryGetValue(FrameworkRuntimeStates.Fallback.Id, out var fallback)
            ? fallback
            : throw new InvalidOperationException(
                $"Framework fallback presentation '{FrameworkRuntimeStates.Fallback.Id}' is not registered.");
        _environment = environment;
    }

    public string GetTitleSuffix(SiteModeContext context)
    {
        return Resolve(context, SiteModePresentationPart.TitleSuffix, module => module.GetTitleSuffix());
    }

    public string GetDefaultMetaDescription(SiteModeContext context)
    {
        return Resolve(
            context,
            SiteModePresentationPart.DefaultMetaDescription,
            module => module.GetDefaultMetaDescription());
    }

    public string GetFaviconPath(SiteModeContext context)
    {
        var faviconPath = Resolve(context, SiteModePresentationPart.Favicon, module => module.GetFaviconPath());
        if (AssetExists(faviconPath))
        {
            return faviconPath;
        }

        return _fallbackModule.GetFaviconPath();
    }

    public string? GetDefaultMetaImagePath(SiteModeContext context)
    {
        return Resolve(
            context,
            SiteModePresentationPart.DefaultMetaImage,
            module => module.GetDefaultMetaImagePath());
    }

    public string? GetStructuredDataJson(SiteModeContext context, string canonicalOrigin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalOrigin);
        return Resolve(
            context,
            SiteModePresentationPart.StructuredData,
            module => module.GetStructuredDataJson(canonicalOrigin.TrimEnd('/')));
    }

    public ArticlesIndexPresentationViewModel GetArticlesIndexPresentation(SiteModeContext context)
    {
        return Resolve(
            context,
            SiteModePresentationPart.ArticlesIndex,
            module => module.GetArticlesIndexPresentation());
    }

    public string GetTitleSuffix(SiteMode siteMode) =>
        GetTitleSuffix(BuildCompatibilityContext(siteMode));

    public string GetDefaultMetaDescription(SiteMode siteMode) =>
        GetDefaultMetaDescription(BuildCompatibilityContext(siteMode));

    public string GetFaviconPath(SiteMode siteMode) =>
        GetFaviconPath(BuildCompatibilityContext(siteMode));

    public string? GetDefaultMetaImagePath(SiteMode siteMode) =>
        GetDefaultMetaImagePath(BuildCompatibilityContext(siteMode));

    public string? GetStructuredDataJson(SiteMode siteMode, string canonicalOrigin) =>
        GetStructuredDataJson(BuildCompatibilityContext(siteMode), canonicalOrigin);

    public ArticlesIndexPresentationViewModel GetArticlesIndexPresentation(SiteMode siteMode) =>
        GetArticlesIndexPresentation(BuildCompatibilityContext(siteMode));

    private T Resolve<T>(
        SiteModeContext context,
        SiteModePresentationPart presentationPart,
        Func<ISiteModePresentationModule, T> resolve)
    {
        ArgumentNullException.ThrowIfNull(context);

        var key = GetPresentationKey(context);
        var module = _modules.GetValueOrDefault(key) ?? _fallbackModule;
        try
        {
            return resolve(module);
        }
        catch (SiteModePresentationPartUnavailableException exception)
            when (exception.PresentationPart == presentationPart
                && !string.Equals(
                    module.PresentationKey,
                    FrameworkRuntimeStates.Fallback.Id,
                    StringComparison.Ordinal))
        {
            return resolve(_fallbackModule);
        }
    }

    private static string GetPresentationKey(SiteModeContext context)
    {
        if (context.ActiveMode is not null)
        {
            return context.ActiveMode.Id;
        }

        if (context.FrameworkState is not null)
        {
            return context.FrameworkState.Id;
        }

        return FrameworkRuntimeStates.Fallback.Id;
    }

    private static SiteModeContext BuildCompatibilityContext(SiteMode siteMode)
    {
        if (BuiltInSiteModes.TryGetByLegacyMode(siteMode, out var definition))
        {
            return new SiteModeContext
            {
                ActiveMode = definition
            };
        }

        if (FrameworkRuntimeStates.TryGetByLegacyMode(siteMode, out var frameworkState))
        {
            return new SiteModeContext
            {
                FrameworkState = frameworkState
            };
        }

        throw new ArgumentOutOfRangeException(nameof(siteMode), siteMode, "Unknown site mode.");
    }

    private bool AssetExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var relativePath = path.StartsWith("~/", StringComparison.Ordinal)
            ? path[2..]
            : path.TrimStart('/');

        return _environment.WebRootFileProvider.GetFileInfo(relativePath).Exists;
    }
}
