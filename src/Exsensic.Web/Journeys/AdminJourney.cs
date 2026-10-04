using System.Globalization;
using Exsensic.Contracts.Admin;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Common;
using Exsensic.Contracts.Enums;
using Exsensic.Web.ApiClients;
using Exsensic.Web.Models.Admin;
using Exsensic.Web.Models.Shared;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Exsensic.Web.Journeys;

/// <summary>
/// Connects the admin workspace to the API (docs/CONTRACTS.md §6): the dashboard, the filtered booking
/// list, and reviewing a request (confirm with a qualified, free staff member, or reject with a reason).
/// The API decides which staff qualify and enforces every rule.
/// </summary>
public sealed class AdminJourney(ApiClient api, BookingJourney bookings) : IAdminJourney
{
    private const string AdminBookings = "/Admin/Bookings";
    private const int PageSize = 20;

    /// <summary>Status counts, the approval queue (oldest first), today's and the next 7 days' bookings.</summary>
    public async Task<ApiResult<AdminDashboardViewModel>> DashboardAsync(CancellationToken cancellationToken)
    {
        var dashboard = await api.SendAsync<DashboardDto>(HttpMethod.Get, "api/v1/admin/dashboard", cancellationToken);
        if (dashboard.Value is not { } d) return new(dashboard.StatusCode, default, false, dashboard.Error);

        var waiting = await api.SendAsync<PagedResult<BookingSummaryDto>>(
            HttpMethod.Get, "api/v1/admin/bookings?status=Requested&page=1&pageSize=10", cancellationToken);
        if (waiting.Value is null) return new(waiting.StatusCode, default, false, waiting.Error);

        var model = new AdminDashboardViewModel
        {
            // Every status appears, even with a count of zero.
            StatusCounts = Enum.GetValues<BookingStatus>().ToDictionary(s => s.ToString(), s => d.StatusCounts.GetValueOrDefault(s)),
            WaitingForApproval = waiting.Value.Items.Select(b => BookingJourney.ToCard(b, AdminBookings)).ToList(),
            Today = d.Today.Select(b => BookingJourney.ToCard(b, AdminBookings)).ToList(),
            NextSevenDays = d.NextSevenDays.Select(b => BookingJourney.ToCard(b, AdminBookings)).ToList(),
            IntegrationAvailable = true,
        };
        return new(dashboard.StatusCode, model, true, null);
    }

    /// <summary>One page of bookings filtered by status and slot date; the API lists the oldest Requested first.</summary>
    public async Task<ApiResult<AdminBookingListViewModel>> ListAsync(string? status, DateOnly? from, DateOnly? to, int page, CancellationToken cancellationToken)
    {
        var filter = Query(status, from, to);
        var result = await api.SendAsync<PagedResult<BookingSummaryDto>>(
            HttpMethod.Get, $"api/v1/admin/bookings?page={page}&pageSize={PageSize}{filter}", cancellationToken);
        if (result.Value is not { } paged) return new(result.StatusCode, default, false, result.Error);

        var totalPages = Math.Max(1, paged.TotalPages);
        var model = new AdminBookingListViewModel
        {
            Status = status,
            From = from,
            To = to,
            Page = page,
            Rows = paged.Items.Select(b => new AdminBookingRowViewModel
            {
                Booking = BookingJourney.ToCard(b, AdminBookings),
                ClientName = string.IsNullOrWhiteSpace(b.ClientCompany) ? b.ClientName : $"{b.ClientName} ({b.ClientCompany})",
            }).ToList(),
            Pager = totalPages > 1 ? new PagerViewModel
            {
                CurrentPage = page,
                TotalPages = totalPages,
                PreviousUrl = page > 1 ? $"{AdminBookings}?page={page - 1}{filter}" : null,
                NextUrl = page < totalPages ? $"{AdminBookings}?page={page + 1}{filter}" : null,
                Label = "Booking pages",
            } : null,
            IntegrationAvailable = true,
        };
        return new(result.StatusCode, model, true, null);
    }

    /// <summary>The booking with its requirements and history, plus the staff the API says are qualified and free.</summary>
    public async Task<ApiResult<AdminReviewViewModel>> ReviewAsync(int id, CancellationToken cancellationToken)
    {
        var result = await api.SendAsync<BookingDetailDto>(HttpMethod.Get, $"api/v1/bookings/{id}", cancellationToken);
        if (result.Value is not { } booking) return new(result.StatusCode, default, false, result.Error);

        var detail = await bookings.ToDetailAsync(booking, cancellationToken, AdminBookings);
        detail.CanReview = booking.Status == BookingStatus.Requested;
        // Reschedule and cancel are client actions on the client's screens.
        detail.CanReschedule = false;
        detail.CanCancel = false;

        IReadOnlyList<SelectListItem> staffOptions = [];
        if (detail.CanReview)
        {
            var staff = await api.SendAsync<List<StaffOptionDto>>(HttpMethod.Get,
                $"api/v1/admin/staff?serviceId={booking.ServiceId}&timeSlotId={booking.TimeSlotId}", cancellationToken);
            if (staff.Value is null) return new(staff.StatusCode, default, false, staff.Error);
            staffOptions = staff.Value
                .Select(s => new SelectListItem($"{s.FullName} — {s.JobTitle}", s.UserId.ToString()))
                .ToList();
        }

        var model = new AdminReviewViewModel
        {
            Id = id,
            Detail = detail,
            StaffOptions = staffOptions,
            Confirm = new ConfirmBookingViewModel { RowVersion = booking.RowVersion },
            Reject = new RejectBookingViewModel { RowVersion = booking.RowVersion },
            IntegrationAvailable = true,
        };
        return new(result.StatusCode, model, true, null);
    }

    /// <summary>PUT /api/v1/admin/bookings/{id}/confirm with the chosen staff member; null on success.</summary>
    public async Task<ApiProblem?> ConfirmAsync(int id, ConfirmBookingViewModel form, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(form.StaffUserId, out var staffUserId))
        {
            return new ApiProblem { StatusCode = 400, Message = "Choose an available staff member." };
        }

        var result = await api.SendJsonNoContentAsync(HttpMethod.Put, $"api/v1/admin/bookings/{id}/confirm",
            new ConfirmBookingRequest(staffUserId, form.RowVersion), cancellationToken);
        return result.Error;
    }

    /// <summary>PUT /api/v1/admin/bookings/{id}/reject with the reason; null on success.</summary>
    public async Task<ApiProblem?> RejectAsync(int id, RejectBookingViewModel form, CancellationToken cancellationToken)
    {
        var result = await api.SendJsonNoContentAsync(HttpMethod.Put, $"api/v1/admin/bookings/{id}/reject",
            new RejectBookingRequest(form.Reason.Trim(), form.RowVersion), cancellationToken);
        return result.Error;
    }

    private static string Query(string? status, DateOnly? from, DateOnly? to) =>
        (string.IsNullOrEmpty(status) ? "" : $"&status={Uri.EscapeDataString(status)}")
        + (from is { } f ? $"&from={f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}" : "")
        + (to is { } t ? $"&to={t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}" : "");
}
