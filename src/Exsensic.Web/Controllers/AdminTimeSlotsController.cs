using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Web.Controllers;

public class AdminTimeSlotsController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
