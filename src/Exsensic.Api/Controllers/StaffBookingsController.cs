using Exsensic.Api.Bookings;
using Exsensic.Api.Security;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Staff;
using Exsensic.Core.Bookings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Api.Controllers;

/// <summary>
/// The staff member's work (docs/CONTRACTS.md §6): their schedule, and marking a booking as delivered.
/// Thin: access is checked here, the rules live in the booking services.
/// </summary>
[ApiController]
[Route("api/v1/staff")]
public sealed class StaffBookingsController : ControllerBase
{
    private readonly StaffBookingService _staff;
    private readonly BookingQueryService _queries;
    private readonly BookingAccessGuard _access;

    /// <summary>
    /// Creates the controller with the staff booking service, the booking queries and the access guard.
    /// </summary>
    public StaffBookingsController(StaffBookingService staff, BookingQueryService queries, BookingAccessGuard access)
    {
        _staff = staff;
        _queries = queries;
        _access = access;
    }

    /// <summary>
    /// The signed-in staff member's Confirmed and Completed bookings between two dates (default: this week),
    /// ordered by date and time, with a short requirements summary each. A range that ends before it starts
    /// or is longer than 62 days is 400 validation_failed.
    /// </summary>
    /// <param name="from">The first date, inclusive (SAST).</param>
    /// <param name="to">The last date, inclusive (SAST).</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    [HttpGet("me/bookings")]
    [Authorize(Policy = AuthorizationPolicies.StaffOnly)]
    public Task<IReadOnlyList<BookingSummaryDto>> MySchedule(DateOnly? from, DateOnly? to, CancellationToken ct) =>
        _queries.ListStaffScheduleAsync(User.GetUserId(), from, to, ct);

    /// <summary>
    /// Marks a booking as delivered. For the assigned staff member or an admin; clients get 403 and other
    /// staff 404. Completing before the slot starts is 409 invalid_transition. Returns the updated booking.
    /// </summary>
    /// <param name="id">The booking id.</param>
    /// <param name="request">The RowVersion from the last read.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    [HttpPut("bookings/{id:int}/complete")]
    [Authorize(Roles = RoleNames.Staff + "," + RoleNames.Admin)]
    public async Task<BookingDetailDto> Complete(int id, CompleteBookingRequest request, CancellationToken ct)
    {
        await _access.EnsureCanAccessAsync(User, id, ct);
        await _staff.CompleteAsync(id, request, User.GetUserId(), ct);
        return await _queries.GetDetailAsync(id, ct);
    }
}
