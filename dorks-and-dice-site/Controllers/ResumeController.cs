using dorks_and_dice_site.Services.Resume;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

public class ResumeController : Controller
{
    private readonly IResumeContentService _resumeContentService;

    public ResumeController(IResumeContentService resumeContentService)
    {
        _resumeContentService = resumeContentService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await _resumeContentService.GetResumePageAsync(cancellationToken);
        return View("~/Views/SiteModes/Professional/Home.cshtml", model);
    }
}
