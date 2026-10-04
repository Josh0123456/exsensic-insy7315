using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Api.Controllers;

public class AdminServicesController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
