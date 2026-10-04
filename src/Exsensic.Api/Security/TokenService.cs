using Exsensic.Contracts.Auth;
using Exsensic.Data;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Exsensic.Api.Security;

/// <summary>
/// Issues the signed access token the Web app stores in its sign-in cookie and sends as
/// "Authorization: Bearer" on every API call.
/// </summary>
public sealed class TokenService(JwtSettings settings, TimeProvider timeProvider)
{
    /// <summary>
    /// Creates a token for the user carrying their id, name, role and current security stamp,
    /// valid for Jwt:LifetimeMinutes.
    /// </summary>
    public AuthResponse CreateFor(ApplicationUser user, string role)
    {
        var expiresAtUtc = timeProvider.GetUtcNow().AddMinutes(settings.LifetimeMinutes);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Expires = expiresAtUtc.UtcDateTime,
            SigningCredentials = new SigningCredentials(settings.SigningKey, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [ExsensicClaims.UserId] = user.Id.ToString(),
                [ExsensicClaims.Name] = user.FullName,
                [ExsensicClaims.Role] = role,
                [ExsensicClaims.SecurityStamp] = user.SecurityStamp ?? string.Empty,
            },
        });

        return new AuthResponse(token, expiresAtUtc, user.Id, user.FullName, role);
    }
}
