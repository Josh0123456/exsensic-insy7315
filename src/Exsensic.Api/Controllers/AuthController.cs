using Exsensic.Api.Security;
using Exsensic.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Exsensic.Api.Controllers;

/// <summary>Public registration and sign-in (docs/CONTRACTS.md §6).</summary>
[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
public sealed class AuthController(AccountService accounts) : ControllerBase
{
    /// <summary>The single message for every sign-in failure, so it never reveals whether an email is registered.</summary>
    public const string LoginFailedMessage = "Email or password is incorrect.";

    /// <summary>Registers a Client account and returns a signed-in token (201).</summary>
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Register)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await accounts.RegisterAsync(request, ct);
        if (result.Value is null)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(result.Field!, error);
            return ValidationProblem(ModelState);
        }

        return Created("/api/v1/me", result.Value);
    }

    /// <summary>Signs in with email and password; 401 with one generic message on any failure.</summary>
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var response = await accounts.LoginAsync(request, ct);
        return response is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: LoginFailedMessage)
            : Ok(response);
    }
}
