using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Api.Controllers;

public class AdminTimeSlotsController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
