using Exsensic.Contracts.Auth;
using Exsensic.Web.ApiClients;
using Exsensic.Web.Journeys;
using Exsensic.Web.Models.Account;
using Exsensic.Web.Models.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Web.Controllers;

/// <summary>Account screens backed by the registered real-auth adapter; never synthesizes a session.</summary>
/// <param name="journey">Maps the merged authentication contracts to account screens.</param>
[Route("Account")]
public sealed class AccountController(IAccountJourney journey) : JourneyController
{
    /// <summary>Shows sign-in and retains only a local return destination.</summary>
    [AllowAnonymous, HttpGet("Login")]
    public IActionResult Login(string? returnUrl)
    {
        // MVC inputs prefer ModelState values over the model: discard the untrusted query value too.
        ModelState.Remove(nameof(returnUrl));
        return View(new LoginViewModel
        {
            ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null, IntegrationAvailable = true
        });
    }

    /// <summary>Signs in only through the real account adapter.</summary>
    [AllowAnonymous, HttpPost("Login")]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        model.ReturnUrl = Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : null;
        ModelState.Remove(nameof(model.ReturnUrl));
        model.IntegrationAvailable = true;
        if (ModelState.IsValid)
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
    public IActionResult Register() => View(new RegisterViewModel { IntegrationAvailable = true });

    /// <summary>Registers through the real API.</summary>
    [AllowAnonymous, HttpPost("Register")]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        model.IntegrationAvailable = true;
        if (ModelState.IsValid)
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
        await RenderAsync("Profile", await journey.GetProfileAsync(cancellationToken), new ProfileViewModel(), cancellationToken);

    /// <summary>Saves editable profile fields through the real adapter.</summary>
    [Authorize, HttpPost("Profile")]
    public async Task<IActionResult> Profile(ProfileViewModel model, CancellationToken cancellationToken)
    {
        model.IntegrationAvailable = true;
        var current = await journey.GetProfileAsync(cancellationToken);
        if (!current.IsSuccess || current.Value is null)
            return await RenderAsync("Profile", current, model, cancellationToken);
        model.Email = current.Value.Email;
        ModelState.Remove(nameof(model.Email));
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
        if (User.IsInRole(RoleNames.Admin)) return RedirectToAction("Index", "Admin");
        if (User.IsInRole(RoleNames.Staff)) return RedirectToAction("Index", "Staff");
        return RedirectToAction("Index", "Services");
    }
}
