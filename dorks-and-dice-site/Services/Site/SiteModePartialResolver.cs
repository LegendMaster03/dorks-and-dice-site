using dorks_and_dice_site.Models.Site;
using Microsoft.AspNetCore.Mvc.ViewEngines;

namespace dorks_and_dice_site.Services.Site;

public sealed class SiteModePartialResolver : ISiteModePartialResolver
{
    private readonly ICompositeViewEngine _viewEngine;

    public SiteModePartialResolver(ICompositeViewEngine viewEngine)
    {
        _viewEngine = viewEngine;
    }

    public string GetPartialPath(SiteModeContext context, string partialName)
    {
        ArgumentNullException.ThrowIfNull(context);

        var preferredFolder = GetPreferredFolder(context);
        var preferredPath = BuildPartialPath(preferredFolder, partialName);
        if (_viewEngine.GetView(executingFilePath: null, viewPath: preferredPath, isMainPage: false).Success)
        {
            return preferredPath;
        }

        return BuildPartialPath(FrameworkRuntimeStates.Fallback.ViewFolder, partialName);
    }

    public string GetBrandingPartialPath(SiteModeContext context, SiteModeBrandingPart brandingPart)
    {
        if (!Enum.IsDefined(brandingPart))
        {
            throw new ArgumentOutOfRangeException(nameof(brandingPart), brandingPart, "Unknown branding part.");
        }

        return GetPartialPath(context, $"Branding/_{brandingPart}");
    }

    public string GetPartialPath(SiteMode siteMode, string partialName) =>
        GetPartialPath(BuildCompatibilityContext(siteMode), partialName);

    public string GetBrandingPartialPath(SiteMode siteMode, SiteModeBrandingPart brandingPart) =>
        GetBrandingPartialPath(BuildCompatibilityContext(siteMode), brandingPart);

    private static string GetPreferredFolder(SiteModeContext context)
    {
        if (context.ActiveMode is not null)
        {
            return context.ActiveMode.ViewFolder;
        }

        if (context.SyntheticMode is not null)
        {
            return context.SyntheticMode.ViewFolder;
        }

        return FrameworkRuntimeStates.Fallback.ViewFolder;
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

    private static string BuildPartialPath(string folderName, string partialName)
    {
        return $"~/Views/SiteModes/{folderName}/{partialName}.cshtml";
    }
}
