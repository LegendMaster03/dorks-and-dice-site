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
            return View();
        }

        public IActionResult SeniorProject()
        {
            return View();
        }

        public IActionResult DirectedIndependentStudy()
        {
            return View();
        }

        public IActionResult CyberSecurityTeam()
        {
            return RedirectToAction(nameof(DirectedIndependentStudy));
        }

        public IActionResult Skyblivion()
        {
            return View();
        }

        public IActionResult Skywind()
        {
            return RedirectToAction(nameof(ExperienceSkywind));
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
            return RedirectToAction(nameof(Skyblivion));
        }

        public IActionResult ExperienceSkywind()
        {
            return View();
        }
    }
}
