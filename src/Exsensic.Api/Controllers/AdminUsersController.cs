using Exsensic.Api.Errors;
using Exsensic.Api.Security;
using Exsensic.Contracts.Admin;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Common;
using Exsensic.Core.Entities;
using Exsensic.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Exsensic.Api.Controllers;

/// <summary>
/// Admin-only user management. Lists users, creates staff accounts, and activates or
/// deactivates accounts. Deactivation bumps the security stamp so live tokens stop working.
/// </summary>
[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminUsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ExsensicDbContext _db;

    /// <summary>
    /// Creates the controller with the Identity user manager and the database context.
    /// </summary>
    public AdminUsersController(UserManager<ApplicationUser> users, ExsensicDbContext db)
    {
        _users = users;
        _db = db;
    }

    /// <summary>
    /// Returns one page of users, optionally filtered by role, ordered by name.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserSummaryDto>>> List(
        string? role, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _users.Users.AsNoTracking();

        // If a role filter is given, narrow the query to the ids in that role.
        if (!string.IsNullOrWhiteSpace(role))
        {
            var usersInRole = await _users.GetUsersInRoleAsync(role);
            var ids = usersInRole.Select(u => u.Id).ToList();
            query = query.Where(u => ids.Contains(u.Id));
        }

        var total = await query.CountAsync(ct);
        var users = await query
            .OrderBy(u => u.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = new List<UserSummaryDto>();
        foreach (var user in users)
        {
            var userRole = (await _users.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty;
            items.Add(new UserSummaryDto(user.Id, user.FullName, user.Email ?? string.Empty, userRole, user.IsActive));
        }

        return Ok(new PagedResult<UserSummaryDto>(items, page, pageSize, total));
    }

    /// <summary>
    /// Creates a Staff account with a profile and service links. Returns 201 with the new user's id.
    /// A duplicate email is a 409 with the code "duplicate".
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateStaff(CreateStaffRequest request, CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var result = await _users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return Conflict(new ProblemDetails
            {
                Status = 409,
                Title = "Could not create staff account.",
                Extensions = { [ErrorHandlingExtensions.CodeKey] = ErrorCodes.Duplicate }
            });
        }

        await _users.AddToRoleAsync(user, RoleNames.Staff);

        _db.StaffProfiles.Add(new StaffProfile
        {
            UserId = user.Id,
            JobTitle = request.JobTitle
        });

        foreach (var serviceId in request.ServiceIds)
        {
            _db.StaffServices.Add(new StaffService
            {
                StaffUserId = user.Id,
                ServiceId = serviceId
            });
        }

        await _db.SaveChangesAsync(ct);
        return StatusCode(201, new { userId = user.Id });
    }

    /// <summary>
    /// Activates or deactivates an account. Bumping the security stamp kills tokens issued before now.
    /// </summary>
    [HttpPut("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, SetUserActiveRequest request, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound(new ProblemDetails { Status = 404, Title = "User not found." });
        }

        user.IsActive = request.IsActive;

        // Any token issued before this moment now fails validation in AuthenticationExtensions.
        await _users.UpdateSecurityStampAsync(user);
        await _users.UpdateAsync(user);

        return NoContent();
    }
}
