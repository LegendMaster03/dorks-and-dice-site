using dorks_and_dice_site.Models.Site;
using Microsoft.AspNetCore.Mvc.ViewEngines;

namespace dorks_and_dice_site.Services.Site;

public sealed class SiteModePartialResolver : ISiteModePartialResolver
{
    private const string UnassignedFolderName = "Unassigned";
    private readonly ICompositeViewEngine _viewEngine;

    public SiteModePartialResolver(ICompositeViewEngine viewEngine)
    {
        _viewEngine = viewEngine;
    }

    public string GetPartialPath(SiteMode siteMode, string partialName)
    {
        var modePath = BuildPartialPath(GetFolderName(siteMode), partialName);
        if (_viewEngine.GetView(executingFilePath: null, viewPath: modePath, isMainPage: false).Success)
        {
            return modePath;
        }

        return BuildPartialPath(UnassignedFolderName, partialName);
    }

    public string GetBrandingPartialPath(SiteMode siteMode, SiteModeBrandingPart brandingPart)
    {
        if (!Enum.IsDefined(brandingPart))
        {
            throw new ArgumentOutOfRangeException(nameof(brandingPart), brandingPart, "Unknown branding part.");
        }

        return GetPartialPath(siteMode, $"Branding/_{brandingPart}");
    }

    private static string BuildPartialPath(string folderName, string partialName)
    {
        return $"~/Views/SiteModes/{folderName}/{partialName}.cshtml";
    }

    private static string GetFolderName(SiteMode siteMode)
    {
        return siteMode switch
        {
            SiteMode.DorksAndDice => "DorksAndDice",
            SiteMode.Professional => "Professional",
            SiteMode.Development => "Development",
            SiteMode.Unassigned => UnassignedFolderName,
            _ => UnassignedFolderName
        };
    }
}
