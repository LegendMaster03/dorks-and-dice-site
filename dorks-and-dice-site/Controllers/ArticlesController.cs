using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers;

[Route("articles")]
public class ArticlesController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("freeing-the-bees-consolevariations-puzzle")]
    public IActionResult FreeingTheBeesConsoleVariationsPuzzle()
    {
        return View();
    }
}
