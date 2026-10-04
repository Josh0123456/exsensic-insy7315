using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Common;
using Exsensic.Core.Entities;
using Exsensic.Core.Exceptions;
using Exsensic.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Exsensic.Api.Security;

/// <summary>
/// The rules for registering, signing in and editing your own profile. Controllers only pass the
/// request in and map the result to HTTP.
/// </summary>
public sealed class AccountService(
    ExsensicDbContext db,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    TokenService tokens,
    TimeProvider timeProvider,
    ILogger<AccountService> logger)
{
    /// <summary>
    /// Creates a Client account and its ClientProfile in one transaction, then signs the user in.
    /// Public registration can only ever create the Client role (docs/CONTRACTS.md §2).
    /// Returns field errors for a weak or mismatched password; throws a 409 duplicate for a taken email.
    /// </summary>
    public async Task<AccountResult<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (request.Password != request.ConfirmPassword)
        {
            return AccountResult<AuthResponse>.Invalid(nameof(request.ConfirmPassword), "The passwords do not match.");
        }

        var email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw new BusinessRuleException(ErrorCodes.Duplicate, "An account with this email already exists.");
        }

        return await db.InTransactionAsync(async ct =>
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = request.FullName.Trim(),
                IsActive = true,
                CreatedAtUtc = timeProvider.GetUtcNow(),
            };

            var created = await userManager.CreateAsync(user, request.Password);
            if (!created.Succeeded)
            {
                // Nothing was saved, so the empty transaction commits harmlessly.
                return AccountResult<AuthResponse>.Invalid(nameof(request.Password), created.Errors.Select(e => e.Description));
            }

            await userManager.AddToRoleAsync(user, RoleNames.Client);
            db.ClientProfiles.Add(new ClientProfile
            {
                UserId = user.Id,
                CompanyName = request.CompanyName.Trim(),
                Phone = request.Phone.Trim(),
            });
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Client account {UserId} registered.", user.Id);
            return AccountResult<AuthResponse>.Ok(tokens.CreateFor(user, RoleNames.Client));
        }, ct);
    }

    /// <summary>
    /// Checks the password with lockout counting (5 failures lock the account for 15 minutes).
    /// Returns null for every kind of failure (unknown email, wrong password, locked or deactivated
    /// account) so the caller can send one generic message and never reveal which accounts exist.
    /// </summary>
    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
        {
            return null;
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            logger.LogInformation("Sign-in failed for user {UserId} (locked out: {LockedOut}).", user.Id, result.IsLockedOut);
            return null;
        }

        var role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? RoleNames.Client;
        return tokens.CreateFor(user, role);
    }

    /// <summary>Returns the signed-in user's profile, including company and phone for clients.</summary>
    public async Task<ProfileDto> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("User");
        var role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? RoleNames.Client;
        var profile = await db.ClientProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct);

        return new ProfileDto(user.Id, user.FullName, user.Email ?? string.Empty, profile?.CompanyName, profile?.Phone, role);
    }

    /// <summary>
    /// Updates the signed-in user's name and, for clients, their company and phone.
    /// Email, role and password are not changed here.
    /// </summary>
    public async Task<ProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("User");
        user.FullName = request.FullName.Trim();

        var profile = await db.ClientProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is not null)
        {
            if (!string.IsNullOrWhiteSpace(request.CompanyName)) profile.CompanyName = request.CompanyName.Trim();
            if (!string.IsNullOrWhiteSpace(request.Phone)) profile.Phone = request.Phone.Trim();
        }

        // UpdateAsync saves the user and the tracked profile change together.
        await userManager.UpdateAsync(user);
        await db.SaveChangesAsync(ct);
        return await GetProfileAsync(userId, ct);
    }
}

/// <summary>Either a value or field errors to return as a 400 validation response.</summary>
public sealed record AccountResult<T>(T? Value, string? Field, IReadOnlyList<string> Errors)
{
    /// <summary>A successful result.</summary>
    public static AccountResult<T> Ok(T value) => new(value, null, []);

    /// <summary>A failed result with errors for one field.</summary>
    public static AccountResult<T> Invalid(string field, IEnumerable<string> errors) => new(default, field, errors.ToList());

    /// <summary>A failed result with a single error for one field.</summary>
    public static AccountResult<T> Invalid(string field, string error) => Invalid(field, [error]);
}
