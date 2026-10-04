using Exsensic.Contracts.Auth;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Exsensic.Web.Journeys;

/// <summary>Creates a protected MVC session only from the account API's successful real API authentication response.</summary>
/// <param name="contextAccessor">Current HTTP request.</param>
/// <param name="clock">Clock for limiting cookie lifetime to the real token lifetime.</param>
public sealed class AccountSession(IHttpContextAccessor contextAccessor, TimeProvider clock)
{
    /// <summary>Stores a real API token in authentication properties and a minimal identity in claims.</summary>
    public async Task SignInAsync(Guid userId, string fullName, string role, string accessToken,
        DateTimeOffset expiresAtUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(accessToken)
            || role is not (RoleNames.Client or RoleNames.Staff or RoleNames.Admin) || expiresAtUtc <= clock.GetUtcNow())
        {
            throw new InvalidOperationException("A valid API authentication response is required.");
        }

        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, fullName),
            new Claim(ClaimTypes.Role, role)
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        var maximumExpiry = clock.GetUtcNow().AddMinutes(30);
        var properties = new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = false,
            ExpiresUtc = expiresAtUtc < maximumExpiry ? expiresAtUtc : maximumExpiry
        };
        properties.StoreTokens([
            new AuthenticationToken { Name = "access_token", Value = accessToken },
            new AuthenticationToken { Name = "expires_at", Value = expiresAtUtc.ToString("O", CultureInfo.InvariantCulture) }
        ]);
        var context = contextAccessor.HttpContext ?? throw new InvalidOperationException("Sign-in requires an HTTP request.");
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), properties);
        context.User = new ClaimsPrincipal(identity);
    }
}
