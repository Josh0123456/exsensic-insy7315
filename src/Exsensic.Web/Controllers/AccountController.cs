using Exsensic.Web.ApiClients;
using Exsensic.Web.Journeys;
using Exsensic.Web.Models.Account;
using Exsensic.Web.Models.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Web.Controllers;

/// <summary>Account screens backed by an optional real-auth adapter; never synthesizes a session.</summary>
/// <param name="journey">Registered only when P3's authentication contracts exist.</param>
[Route("Account")]
public sealed class AccountController(IAccountJourney? journey = null) : JourneyController
{
    /// <summary>Shows sign-in and retains only a local return destination.</summary>
    [AllowAnonymous, HttpGet("Login")]
    public IActionResult Login(string? returnUrl) => View(new LoginViewModel
    {
        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null, IntegrationAvailable = journey is not null
    });

    /// <summary>Signs in only through the real account adapter.</summary>
    [AllowAnonymous, HttpPost("Login")]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        model.ReturnUrl = Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : null;
        ModelState.Remove(nameof(model.ReturnUrl));
        model.IntegrationAvailable = journey is not null;
        if (journey is null) IntegrationError();
        if (ModelState.IsValid && journey is not null)
        {
            var problem = await journey.LoginAsync(model, cancellationToken);
            if (problem is null) return SignedInDestination(model.ReturnUrl);
            if (problem.StatusCode == 401)
                ModelState.AddModelError(string.Empty, "We could not sign you in. Please check your email and password.");
            else problem.AddToModelState(ModelState);
            model.Problem = problem;
        }
        model.Password = "";
        ModelState.SetModelValue(nameof(model.Password), null, null);
        return View(model);
    }

    /// <summary>Shows public Client registration without role selection.</summary>
    [AllowAnonymous, HttpGet("Register")]
    public IActionResult Register() => View(new RegisterViewModel { IntegrationAvailable = journey is not null });

    /// <summary>Registers through the real API; a missing dependency cannot produce success.</summary>
    [AllowAnonymous, HttpPost("Register")]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        model.IntegrationAvailable = journey is not null;
        if (journey is null) IntegrationError();
        if (ModelState.IsValid && journey is not null)
        {
            var problem = await journey.RegisterAsync(model, cancellationToken);
            if (problem is null) return SignedInDestination(null);
            problem.AddToModelState(ModelState);
            model.Problem = problem;
        }
        model.Password = model.ConfirmPassword = "";
        ModelState.SetModelValue(nameof(model.Password), null, null);
        ModelState.SetModelValue(nameof(model.ConfirmPassword), null, null);
        return View(model);
    }

    /// <summary>Loads the current user's real profile without exposing token data.</summary>
    [Authorize, HttpGet("Profile")]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken) =>
        journey is null ? Unavailable("Profile", new ProfileViewModel())
        : await RenderAsync("Profile", await journey.GetProfileAsync(cancellationToken), new ProfileViewModel(), cancellationToken);

    /// <summary>Saves editable profile fields through the real adapter.</summary>
    [Authorize, HttpPost("Profile")]
    public async Task<IActionResult> Profile(ProfileViewModel model, CancellationToken cancellationToken)
    {
        model.IntegrationAvailable = journey is not null;
        if (journey is null) IntegrationError();
        if (journey is not null)
        {
            var current = await journey.GetProfileAsync(cancellationToken);
            if (!current.IsSuccess || current.Value is null)
                return await RenderAsync("Profile", current, model, cancellationToken);
            model.ShowCompany = current.Value.ShowCompany;
            if (!model.ShowCompany) model.CompanyName = null;
            if (ModelState.IsValid)
            {
                var problem = await journey.SaveProfileAsync(model, cancellationToken);
                if (problem is null)
                {
                    TempData[ToastKeys.Success] = "Your profile has been updated.";
                    return RedirectToAction(nameof(Profile));
                }
                var special = await HandleProblemAsync(problem, cancellationToken);
                if (special is not null) return special;
                model.Problem = problem;
            }
        }
        return View(model);
    }

    /// <summary>Clears the MVC authentication ticket and its stored API token.</summary>
    [Authorize, HttpPost("Logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    /// <summary>Shows a generic denial without identifying protected resources.</summary>
    [AllowAnonymous, HttpGet("AccessDenied")]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = 403;
        return View();
    }

    private IActionResult SignedInDestination(string? returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl!);
        if (User.IsInRole("Admin")) return RedirectToAction("Index", "Admin");
        if (User.IsInRole("Staff")) return RedirectToAction("Index", "Staff");
        return RedirectToAction("Index", "Services");
    }
}
