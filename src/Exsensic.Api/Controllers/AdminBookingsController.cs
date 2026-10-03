using System.ComponentModel.DataAnnotations;
using Exsensic.Api.Security;
using Exsensic.Contracts.Admin;
using Exsensic.Contracts.Bookings;
using Exsensic.Core.Bookings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Exsensic.Api.Controllers;

/// <summary>
/// The admin's booking review (docs/CONTRACTS.md §6): which staff can take a booking, and approving or
/// rejecting it. Admins only; other roles get 403. Thin: the rules live in <see cref="AdminBookingService"/>.
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminBookingsController : ControllerBase
{
    private readonly AdminBookingService _admin;
    private readonly BookingQueryService _queries;

    /// <summary>
    /// Creates the controller with the admin booking service and the booking queries.
    /// </summary>
    public AdminBookingsController(AdminBookingService admin, BookingQueryService queries)
    {
        _admin = admin;
        _queries = queries;
    }

    /// <summary>
    /// Staff who are qualified for the service and free at the slot's time, for the approval form.
    /// Missing or invalid ids are 400; a service or slot that doesn't exist is 404.
    /// </summary>
    /// <param name="serviceId">The booked service.</param>
    /// <param name="timeSlotId">The booked slot.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    [HttpGet("staff")]
    public Task<IReadOnlyList<StaffOptionDto>> StaffOptions(
        [FromQuery, BindRequired, Range(1, int.MaxValue)] int serviceId,
        [FromQuery, BindRequired, Range(1, int.MaxValue)] int timeSlotId,
        CancellationToken ct) =>
        _queries.ListStaffOptionsAsync(serviceId, timeSlotId, ct);

    /// <summary>
    /// Approves a Requested booking and assigns the chosen staff member. Returns the updated booking.
    /// </summary>
    /// <param name="id">The booking id.</param>
    /// <param name="request">The staff member and the RowVersion from the last read.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    [HttpPut("bookings/{id:int}/confirm")]
    public async Task<BookingDetailDto> Confirm(int id, ConfirmBookingRequest request, CancellationToken ct)
    {
        await _admin.ConfirmAsync(id, request, User.GetUserId(), ct);
        return await _queries.GetDetailAsync(id, ct);
    }

    /// <summary>
    /// Rejects a Requested booking with a reason the client will see. Returns the updated booking.
    /// </summary>
    /// <param name="id">The booking id.</param>
    /// <param name="request">The reason and the RowVersion from the last read.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    [HttpPut("bookings/{id:int}/reject")]
    public async Task<BookingDetailDto> Reject(int id, RejectBookingRequest request, CancellationToken ct)
    {
        await _admin.RejectAsync(id, request, User.GetUserId(), ct);
        return await _queries.GetDetailAsync(id, ct);
    }
}
