using System.Security.Cryptography;
using System.Text;
using Exsensic.Contracts.Auth;
using Exsensic.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace Exsensic.Api.Security;

/// <summary>Names of the claims inside an Exsensic access token.</summary>
public static class ExsensicClaims
{
    /// <summary>The user's id (Guid).</summary>
    public const string UserId = "sub";

    /// <summary>The user's full name.</summary>
    public const string Name = "name";

    /// <summary>The user's role: Client, Staff or Admin.</summary>
    public const string Role = "role";

    /// <summary>
    /// Identity's security stamp when the token was issued. Deactivating an account or changing its
    /// password changes the stamp, so older tokens stop working straight away.
    /// </summary>
    public const string SecurityStamp = "stamp";
}

/// <summary>
/// Sets up sign-in for the API: ASP.NET Core Identity for users and roles, JWT bearer tokens for
/// requests from the Web app, the role policies, and "signed in by default" for every endpoint.
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Registers Identity (PBKDF2 password hashing, docs/CONTRACTS.md §12 decision 6), JWT validation
    /// from the Jwt:* settings, and the authorisation policies in <see cref="AuthorizationPolicies"/>.
    /// </summary>
    public static IServiceCollection AddExsensicAuthentication(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ExsensicDbContext>()
            .AddSignInManager();

        var jwt = JwtSettings.From(configuration, environment);
        services.AddSingleton(jwt);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<TokenService>();
        services.AddScoped<AccountService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep the short claim names ("sub", "role") exactly as issued.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.SigningKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = ExsensicClaims.Name,
                    RoleClaimType = ExsensicClaims.Role,
                };
                options.Events = new JwtBearerEvents { OnTokenValidated = RejectStaleTokensAsync };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.ClientOnly, policy => policy.RequireRole(RoleNames.Client))
            .AddPolicy(AuthorizationPolicies.StaffOnly, policy => policy.RequireRole(RoleNames.Staff))
            .AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy.RequireRole(RoleNames.Admin))
            .AddPolicy(AuthorizationPolicies.StaffOrAdmin, policy => policy.RequireRole(RoleNames.Staff, RoleNames.Admin))
            // Every endpoint needs a signed-in user unless it says [AllowAnonymous] (docs/CONTRACTS.md §2).
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    /// <summary>
    /// Rejects a token whose user no longer exists, is deactivated, or whose security stamp has changed
    /// since the token was issued (for example after an admin deactivated the account).
    /// </summary>
    private static async Task RejectStaleTokensAsync(TokenValidatedContext context)
    {
        var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var userId = context.Principal?.FindFirst(ExsensicClaims.UserId)?.Value;
        var stamp = context.Principal?.FindFirst(ExsensicClaims.SecurityStamp)?.Value;

        var user = userId is null ? null : await userManager.FindByIdAsync(userId);
        if (user is null || !user.IsActive || user.SecurityStamp != stamp)
        {
            context.Fail("The access token is no longer valid.");
        }
    }
}

/// <summary>
/// The Jwt:* settings (docs/CONTRACTS.md §9). The signing key is a secret (user-secrets locally,
/// an App Service setting in Azure) and must be at least 32 characters.
/// </summary>
public sealed record JwtSettings(string Issuer, string Audience, int LifetimeMinutes, SymmetricSecurityKey SigningKey)
{
    /// <summary>
    /// Reads and checks the settings. Outside Development a missing or short key stops the API from
    /// starting. In Development only, a random key is used so the API runs before user-secrets are set
    /// (tokens then stop working when the API restarts).
    /// </summary>
    public static JwtSettings From(IConfiguration configuration, IHostEnvironment environment)
    {
        var key = configuration["Jwt:SigningKey"];
        if (string.IsNullOrEmpty(key) || key.Length < 32)
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException("Jwt:SigningKey must be set and at least 32 characters long.");
            }

            key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        }

        return new JwtSettings(
            configuration["Jwt:Issuer"] ?? "exsensic-api",
            configuration["Jwt:Audience"] ?? "exsensic-web",
            configuration.GetValue("Jwt:LifetimeMinutes", 60),
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)));
    }
}

/// <summary>Reads Exsensic claims from the signed-in user.</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>The signed-in user's id. Only call on endpoints that require sign-in.</summary>
    public static Guid GetUserId(this System.Security.Claims.ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirst(ExsensicClaims.UserId)?.Value
            ?? throw new InvalidOperationException("The request has no signed-in user."));
}
