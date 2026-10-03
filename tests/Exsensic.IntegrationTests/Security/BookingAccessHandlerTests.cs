using System.Security.Claims;
using Exsensic.Api.Security;
using Exsensic.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;

namespace Exsensic.IntegrationTests.Security;

/// <summary>
/// Proves the BookingAccess rule: only an admin, the owning client or the assigned staff member
/// may see a booking. Everyone else is denied, which the booking endpoints turn into 404.
/// </summary>
public class BookingAccessHandlerTests
{
    private static readonly Guid Client = Guid.NewGuid();
    private static readonly Guid Staff = Guid.NewGuid();
    private static readonly BookingAccessResource Booking = new(Client, Staff);

    /// <summary>The client who made the booking is allowed.</summary>
    [Fact]
    public async Task Handle_OwningClient_Succeeds() =>
        Assert.True(await IsAllowedAsync(UserWith(Client, RoleNames.Client), Booking));

    /// <summary>The staff member assigned to the booking is allowed.</summary>
    [Fact]
    public async Task Handle_AssignedStaff_Succeeds() =>
        Assert.True(await IsAllowedAsync(UserWith(Staff, RoleNames.Staff), Booking));

    /// <summary>Any admin is allowed.</summary>
    [Fact]
    public async Task Handle_Admin_Succeeds() =>
        Assert.True(await IsAllowedAsync(UserWith(Guid.NewGuid(), RoleNames.Admin), Booking));

    /// <summary>Another client (IDOR attempt) is denied.</summary>
    [Fact]
    public async Task Handle_OtherClient_IsDenied() =>
        Assert.False(await IsAllowedAsync(UserWith(Guid.NewGuid(), RoleNames.Client), Booking));

    /// <summary>A staff member who is not assigned is denied, including before anyone is assigned.</summary>
    [Fact]
    public async Task Handle_UnassignedStaff_IsDenied()
    {
        Assert.False(await IsAllowedAsync(UserWith(Guid.NewGuid(), RoleNames.Staff), Booking));
        Assert.False(await IsAllowedAsync(UserWith(Staff, RoleNames.Staff), Booking with { StaffUserId = null }));
    }

    /// <summary>A request without a user id is denied.</summary>
    [Fact]
    public async Task Handle_NoUserId_IsDenied() =>
        Assert.False(await IsAllowedAsync(new ClaimsPrincipal(new ClaimsIdentity()), Booking));

    private static ClaimsPrincipal UserWith(Guid id, string role) =>
        new(new ClaimsIdentity(
            [new Claim(ExsensicClaims.UserId, id.ToString()), new Claim(ExsensicClaims.Role, role)],
            authenticationType: "Test", nameType: ExsensicClaims.Name, roleType: ExsensicClaims.Role));

    private static async Task<bool> IsAllowedAsync(ClaimsPrincipal user, BookingAccessResource resource)
    {
        var requirement = new BookingAccessRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, resource);
        await new BookingAccessHandler().HandleAsync(context);
        return context.HasSucceeded;
    }
}
