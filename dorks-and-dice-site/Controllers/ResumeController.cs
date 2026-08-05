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
            return View("~/Views/SiteModes/Professional/Home.cshtml", _resumeContentService.GetResumePage());
        }

        public IActionResult XnGine()
        {
            return View("~/Views/SiteModes/Professional/Resume/Projects/XnGine.cshtml");
        }

        public IActionResult SeniorProject()
        {
            return View("~/Views/SiteModes/Professional/Resume/Projects/SeniorProject.cshtml");
        }

        public IActionResult PythonFinanceAnalytics()
        {
            return View("~/Views/SiteModes/Professional/Resume/Projects/PythonFinanceAnalytics.cshtml");
        }

        public IActionResult PersonalMultiModeWebsite()
        {
            return View("~/Views/SiteModes/Professional/Resume/Projects/PersonalMultiModeWebsite.cshtml");
        }

        public IActionResult DirectedIndependentStudy()
        {
            return View("~/Views/SiteModes/Professional/Resume/Projects/DirectedIndependentStudy.cshtml");
        }

        public IActionResult CyberSecurityTeam()
        {
            return RedirectToAction(nameof(DirectedIndependentStudy));
        }

        public IActionResult Skyblivion()
        {
            return View("~/Views/SiteModes/Professional/Resume/Projects/Skyblivion.cshtml");
        }

        public IActionResult Skywind()
        {
            return View("~/Views/SiteModes/Professional/Resume/Projects/Skywind.cshtml");
        }

        public IActionResult TechnologyServices()
        {
            return View("~/Views/SiteModes/Professional/Resume/Projects/TechnologyServices.cshtml");
        }

        public IActionResult SimLabExpo()
        {
            return View("~/Views/SiteModes/Professional/Resume/Projects/SimLabExpo.cshtml");
        }

        public IActionResult WiredWorks()
        {
            return View("~/Views/SiteModes/Professional/Resume/Experience/WiredWorks.cshtml");
        }

        public IActionResult DndTools()
        {
            return View("~/Views/SiteModes/Professional/Resume/Projects/DndTools.cshtml");
        }

        public IActionResult ExperienceCyberSecurityTeam()
        {
            return View("~/Views/SiteModes/Professional/Resume/Experience/ExperienceCyberSecurityTeam.cshtml");
        }

        public IActionResult ExperienceTechnologyServices()
        {
            return View("~/Views/SiteModes/Professional/Resume/Experience/ExperienceTechnologyServices.cshtml");
        }

        public IActionResult ExperienceSimLab()
        {
            return View("~/Views/SiteModes/Professional/Resume/Experience/ExperienceSimLab.cshtml");
        }

        public IActionResult ExperienceWiredWorks()
        {
            return View("~/Views/SiteModes/Professional/Resume/Experience/ExperienceWiredWorks.cshtml");
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
