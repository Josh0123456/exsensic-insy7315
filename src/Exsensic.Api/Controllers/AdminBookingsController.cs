using System.ComponentModel.DataAnnotations;
using Exsensic.Api.Security;
using Exsensic.Contracts.Admin;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Common;
using Exsensic.Contracts.Enums;
using Exsensic.Core.Bookings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Exsensic.Api.Controllers;

/// <summary>
/// The admin's booking overview and review (docs/CONTRACTS.md §6): the dashboard, the booking list,
/// which staff can take a booking, and approving or rejecting it. Admins only; other roles get 403.
/// Thin: the rules live in <see cref="AdminBookingService"/> and <see cref="BookingQueryService"/>.
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
    /// The admin dashboard: a count for every status, how many requests are waiting, and today's and the
    /// next seven days' bookings.
    /// </summary>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    [HttpGet("dashboard")]
    public Task<DashboardDto> Dashboard(CancellationToken ct) => _queries.GetDashboardAsync(ct);

    /// <summary>
    /// Every booking, filtered by status and slot date and paged. Requested bookings come first, oldest
    /// request first, so the approval queue is worked in order. An unknown status, or a range that ends
    /// before it starts, is 400 validation_failed.
    /// </summary>
    /// <param name="status">Optional status filter.</param>
    /// <param name="from">Optional first slot date, inclusive (SAST).</param>
    /// <param name="to">Optional last slot date, inclusive (SAST).</param>
    /// <param name="page">Page number, from 1.</param>
    /// <param name="pageSize">Items per page, default 20, at most 100.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    [HttpGet("bookings")]
    public Task<PagedResult<BookingSummaryDto>> Bookings(
        BookingStatus? status,
        DateOnly? from,
        DateOnly? to,
        int page = 1,
        int pageSize = BookingQueryService.DefaultPageSize,
        CancellationToken ct = default) =>
        _queries.ListForAdminAsync(status, from, to, page, pageSize, ct);

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
