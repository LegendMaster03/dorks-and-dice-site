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

        public IActionResult Resume()
        {
            return RedirectToAction(nameof(ResumeController.Index), "Resume");
        }

        public IActionResult XnGine()
        {
            return RedirectToAction(nameof(ResumeController.XnGine), "Resume");
        }

        public IActionResult SeniorProject()
        {
            return RedirectToAction(nameof(ResumeController.SeniorProject), "Resume");
        }

        public IActionResult CyberSecurityTeam()
        {
            return RedirectToAction(nameof(ResumeController.CyberSecurityTeam), "Resume");
        }

        public IActionResult Skyblivion()
        {
            return RedirectToAction(nameof(ResumeController.Skyblivion), "Resume");
        }

        public IActionResult Skywind()
        {
            return RedirectToAction(nameof(ResumeController.Skywind), "Resume");
        }

        public IActionResult TechnologyServices()
        {
            return RedirectToAction(nameof(ResumeController.TechnologyServices), "Resume");
        }

        public IActionResult SimLabExpo()
        {
            return RedirectToAction(nameof(ResumeController.SimLabExpo), "Resume");
        }

        public IActionResult WiredWorks()
        {
            return RedirectToAction(nameof(ResumeController.WiredWorks), "Resume");
        }

        public IActionResult DndTools()
        {
            return RedirectToAction(nameof(ResumeController.DndTools), "Resume");
        }

        public IActionResult ExperienceCyberSecurityTeam()
        {
            return RedirectToAction(nameof(ResumeController.ExperienceCyberSecurityTeam), "Resume");
        }

        public IActionResult ExperienceTechnologyServices()
        {
            return RedirectToAction(nameof(ResumeController.ExperienceTechnologyServices), "Resume");
        }

        public IActionResult ExperienceSimLab()
        {
            return RedirectToAction(nameof(ResumeController.ExperienceSimLab), "Resume");
        }

        public IActionResult ExperienceWiredWorks()
        {
            return RedirectToAction(nameof(ResumeController.ExperienceWiredWorks), "Resume");
        }

        public IActionResult ExperienceSkyblivion()
        {
            return RedirectToAction(nameof(ResumeController.ExperienceSkyblivion), "Resume");
        }

        public IActionResult ExperienceSkywind()
        {
            return RedirectToAction(nameof(ResumeController.ExperienceSkywind), "Resume");
        }

        public IActionResult NotFoundPage()
        {
            Response.StatusCode = 404;
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
