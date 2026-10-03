using Exsensic.Data;
using Microsoft.AspNetCore.Identity;

namespace Exsensic.Api.Security;

/// <summary>
/// Sets up ASP.NET Core Identity for the API: users and roles stored by EF Core, with the password
/// and lockout rules from the security design. JWT bearer authentication and the authorisation
/// policies are added to this same method in the authentication step (Dean's step 8).
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Registers Identity's user and role managers backed by <see cref="ExsensicDbContext"/>.
    /// Passwords use Identity's default PBKDF2 hashing (docs/CONTRACTS.md §12, decision 6).
    /// </summary>
    public static IServiceCollection AddExsensicAuthentication(this IServiceCollection services, IConfiguration configuration)
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
            .AddEntityFrameworkStores<ExsensicDbContext>();

        return services;
    }
}
