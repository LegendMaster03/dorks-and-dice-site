using dorks_and_dice_site.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace dorks_and_dice_site.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;

        public HomeController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            ViewData["DiscordWidgetUrl"] = _configuration["Discord:WidgetUrl"];
            return View();
        }

        public IActionResult Projects()
        {
            return View();
        }

        public IActionResult Resume()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult DorksAndDice()
        {
            return View();
        }

        public IActionResult XnGine()
        {
            return View();
        }

        public IActionResult SeniorProject()
        {
            return View();
        }

        public IActionResult CyberSecurityTeam()
        {
            return View();
        }

        public IActionResult Skyblivion()
        {
            return View();
        }

        public IActionResult Skywind()
        {
            return View();
        }

        public IActionResult TechnologyServices()
        {
            return View();
        }

        public IActionResult SimLabExpo()
        {
            return View();
        }

        public IActionResult WiredWorks()
        {
            return View();
        }

        public IActionResult DndTools()
        {
            return View();
        }

        public IActionResult ExperienceCyberSecurityTeam()
        {
            return View();
        }

        public IActionResult ExperienceTechnologyServices()
        {
            return View();
        }

        public IActionResult ExperienceSimLab()
        {
            return View();
        }

        public IActionResult ExperienceWiredWorks()
        {
            return View();
        }

        public IActionResult ExperienceSkyblivion()
        {
            return View();
        }

        public IActionResult ExperienceSkywind()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
