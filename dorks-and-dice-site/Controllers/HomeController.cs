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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
