using Exsensic.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;

namespace Exsensic.Api.Security;

/// <summary>
/// Who a booking belongs to: the facts the BookingAccess rule needs. Load just these two ids for the
/// booking, then call IAuthorizationService.AuthorizeAsync(User, resource, AuthorizationPolicies.BookingAccess).
/// If it fails (or the booking is missing), return 404 not_found, never 403, so callers cannot tell
/// that someone else's booking exists (docs/CONTRACTS.md §12, decision 10).
/// </summary>
/// <param name="ClientUserId">The client who made the booking.</param>
/// <param name="StaffUserId">The staff member assigned to it, or null before it is confirmed.</param>
public sealed record BookingAccessResource(Guid ClientUserId, Guid? StaffUserId);

/// <summary>The requirement behind the BookingAccess policy.</summary>
public sealed class BookingAccessRequirement : IAuthorizationRequirement;

/// <summary>
/// Resource-based authorisation for bookings: allowed for an admin, the owning client, or the
/// assigned staff member. Only works through IAuthorizationService with a <see cref="BookingAccessResource"/>;
/// putting [Authorize(Policy = BookingAccess)] on an action alone always denies.
/// </summary>
public sealed class BookingAccessHandler : AuthorizationHandler<BookingAccessRequirement, BookingAccessResource>
{
    /// <summary>Succeeds when the signed-in user is an admin, the booking's client, or its assigned staff member.</summary>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, BookingAccessRequirement requirement, BookingAccessResource resource)
    {
        var userId = context.User.FindFirst(ExsensicClaims.UserId)?.Value;
        var isAllowed =
            context.User.IsInRole(RoleNames.Admin)
            || (userId is not null && Guid.TryParse(userId, out var id)
                && (id == resource.ClientUserId || id == resource.StaffUserId));

        if (isAllowed)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
