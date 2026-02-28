using dorks_and_dice_site.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace dorks_and_dice_site.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Projects()
        {
            return Redirect("/Home/Resume#projects");
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
