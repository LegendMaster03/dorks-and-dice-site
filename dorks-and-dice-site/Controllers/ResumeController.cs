using dorks_and_dice_site.Services.Resume;
using Microsoft.AspNetCore.Mvc;

namespace dorks_and_dice_site.Controllers
{
    public class ResumeController : Controller
    {
        private readonly IResumeContentService _resumeContentService;

        public ResumeController(IResumeContentService resumeContentService)
        {
            _resumeContentService = resumeContentService;
        }

        public IActionResult Index()
        {
            return View(_resumeContentService.GetResumePage());
        }

        public IActionResult XnGine()
        {
            return View("Projects/XnGine");
        }

        public IActionResult SeniorProject()
        {
            return View("Projects/SeniorProject");
        }

        public IActionResult PythonFinanceAnalytics()
        {
            return View("Projects/PythonFinanceAnalytics");
        }

        public IActionResult DirectedIndependentStudy()
        {
            return View("Projects/DirectedIndependentStudy");
        }

        public IActionResult CyberSecurityTeam()
        {
            return RedirectToAction(nameof(DirectedIndependentStudy));
        }

        public IActionResult Skyblivion()
        {
            return View("Projects/Skyblivion");
        }

        public IActionResult Skywind()
        {
            return View("Projects/Skywind");
        }

        public IActionResult TechnologyServices()
        {
            return View("Projects/TechnologyServices");
        }

        public IActionResult SimLabExpo()
        {
            return View("Projects/SimLabExpo");
        }

        public IActionResult WiredWorks()
        {
            return View("Experience/WiredWorks");
        }

        public IActionResult DndTools()
        {
            return View("Projects/DndTools");
        }

        public IActionResult ExperienceCyberSecurityTeam()
        {
            return View("Experience/ExperienceCyberSecurityTeam");
        }

        public IActionResult ExperienceTechnologyServices()
        {
            return View("Experience/ExperienceTechnologyServices");
        }

        public IActionResult ExperienceSimLab()
        {
            return View("Experience/ExperienceSimLab");
        }

        public IActionResult ExperienceWiredWorks()
        {
            return View("Experience/ExperienceWiredWorks");
        }

        public IActionResult ExperienceSkyblivion()
        {
            return RedirectToAction(nameof(Skyblivion));
        }

        public IActionResult ExperienceSkywind()
        {
            return RedirectToAction(nameof(Skywind));
        }
    }
}
