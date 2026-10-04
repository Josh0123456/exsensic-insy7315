using Exsensic.Contracts.Admin;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Common;

namespace Exsensic.Web.ApiClients;

/// <summary>Typed access to the merged adminbookings API contracts; the API owns validation and authorization.</summary>
public interface IAdminBookingsApi
{
    /// <summary>GET /api/v1/admin/dashboard.</summary>
    Task<ApiResult<DashboardDto>> DashboardAsync(CancellationToken cancellationToken);

    /// <summary>GET /api/v1/admin/bookings.</summary>
    Task<ApiResult<PagedResult<BookingSummaryDto>>> ListAsync(string? status, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>GET /api/v1/admin/staff.</summary>
    Task<ApiResult<List<StaffOptionDto>>> StaffAsync(int serviceId, int timeSlotId, CancellationToken cancellationToken);

    /// <summary>PUT /api/v1/admin/bookings/{id}/confirm.</summary>
    Task<ApiResult<BookingDetailDto>> ConfirmAsync(int id, ConfirmBookingRequest request, CancellationToken cancellationToken);

    /// <summary>PUT /api/v1/admin/bookings/{id}/reject.</summary>
    Task<ApiResult<BookingDetailDto>> RejectAsync(int id, RejectBookingRequest request, CancellationToken cancellationToken);

}

/// <summary>Sends shared DTOs through the common authenticated transport.</summary>
public sealed class AdminBookingsApi(ApiClient api) : IAdminBookingsApi
{
    /// <inheritdoc />
    public Task<ApiResult<DashboardDto>> DashboardAsync(CancellationToken cancellationToken) =>
        api.SendAsync<DashboardDto>(HttpMethod.Get, "api/v1/admin/dashboard", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<BookingSummaryDto>>> ListAsync(string? status, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken cancellationToken) =>
        api.SendAsync<PagedResult<BookingSummaryDto>>(HttpMethod.Get, $"api/v1/admin/bookings?page={page}&pageSize={pageSize}{Filter(status, from, to)}", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<List<StaffOptionDto>>> StaffAsync(int serviceId, int timeSlotId, CancellationToken cancellationToken) =>
        api.SendAsync<List<StaffOptionDto>>(HttpMethod.Get, $"api/v1/admin/staff?serviceId={serviceId}&timeSlotId={timeSlotId}", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<BookingDetailDto>> ConfirmAsync(int id, ConfirmBookingRequest request, CancellationToken cancellationToken) =>
        api.SendJsonAsync<ConfirmBookingRequest, BookingDetailDto>(HttpMethod.Put, $"api/v1/admin/bookings/{id}/confirm", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<BookingDetailDto>> RejectAsync(int id, RejectBookingRequest request, CancellationToken cancellationToken) =>
        api.SendJsonAsync<RejectBookingRequest, BookingDetailDto>(HttpMethod.Put, $"api/v1/admin/bookings/{id}/reject", request, cancellationToken);

    private static string Filter(string? status, DateOnly? from, DateOnly? to) =>
        (string.IsNullOrEmpty(status) ? "" : $"&status={Uri.EscapeDataString(status)}")
        + (from is { } f ? "&from=" + f.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : "")
        + (to is { } t ? "&to=" + t.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : "");
}
