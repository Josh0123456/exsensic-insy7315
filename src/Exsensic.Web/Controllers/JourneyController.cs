using Exsensic.Web.ApiClients;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Web.Controllers;

/// <summary>Shared safe feedback for Person 1 MVC journeys; no API calls or business rules.</summary>
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class JourneyController : Controller
{
    /// <summary>Renders only real successful data, with safe handling for failed reads.</summary>
    protected async Task<IActionResult> RenderAsync<T>(string view, ApiResult<T> result, T fallback,
        CancellationToken cancellationToken) where T : JourneyPage
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (result.IsSuccess && result.HasContent && result.Value is not null)
        {
            result.Value.IntegrationAvailable = true;
            return View(view, result.Value);
        }
        var problem = result.Error ?? new ApiProblem { Message = "The service returned no page data. Please try again." };
        var special = await HandleProblemAsync(problem, cancellationToken);
        if (special is not null) return special;
        fallback.IntegrationAvailable = false;
        fallback.Problem = problem;
        return View(view, fallback);
    }

    /// <summary>Handles protected-resource errors without exposing the underlying item.</summary>
    protected async Task<IActionResult?> HandleProblemAsync(ApiProblem problem, CancellationToken cancellationToken, string prefix = "")
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (problem.StatusCode == 401)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData[ToastKeys.Error] = "Your session has expired. Please sign in again.";
            var returnUrl = HttpMethods.IsGet(Request.Method) ? Request.Path.Value + Request.QueryString.Value
                : Url.Action("Index", RouteData.Values["controller"]?.ToString());
            return RedirectToAction("Login", "Account", new { returnUrl });
        }
        if (problem.StatusCode == 403) return Forbid();
        if (problem.StatusCode == 404)
        {
            Response.StatusCode = 404;
            return View("~/Views/Shared/NotFound.cshtml");
        }
        problem.AddToModelState(ModelState, prefix);
        return null;
    }

}
