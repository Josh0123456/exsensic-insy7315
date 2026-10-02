using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Exsensic.Web.Models;

namespace Exsensic.Web.Controllers;

/// <summary>Public Exsensic introduction, privacy information and safe error screen.</summary>
public class HomeController : Controller
{
    /// <summary>Introduces creative services and links to the live catalogue route.</summary>
    [HttpGet("/")]
    public IActionResult Index()
    {
        return View();
    }

    /// <summary>Displays the site's privacy information.</summary>
    [HttpGet("/Privacy")]
    public IActionResult Privacy()
    {
        return View();
    }

    /// <summary>Displays a generic error and correlation identifier without exception details.</summary>
    [Route("/Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
