using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

public class ResumeController : Controller
{
    private readonly ISiteModeHomeService _siteModeHomeService;

    public ResumeController(ISiteModeHomeService siteModeHomeService)
    {
        _siteModeHomeService = siteModeHomeService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var modeContext = HttpContext.GetSiteModeContext();
        var home = await _siteModeHomeService.GetHomeAsync(modeContext, cancellationToken);

        if (home.ViewData is not null)
        {
            foreach (var (key, value) in home.ViewData)
            {
                ViewData[key] = value;
            }
        }

        return home.Model is null
            ? View(home.ViewPath)
            : View(home.ViewPath, home.Model);
    }
}
