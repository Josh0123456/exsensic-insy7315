using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Staff;

namespace Exsensic.Web.ApiClients;

/// <summary>Typed access to the merged staff API contracts; the API owns validation and authorization.</summary>
public interface IStaffApi
{
    /// <summary>GET /api/v1/staff/me/bookings.</summary>
    Task<ApiResult<List<BookingSummaryDto>>> ScheduleAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>PUT /api/v1/staff/bookings/{id}/complete.</summary>
    Task<ApiResult<BookingDetailDto>> CompleteAsync(int id, CompleteBookingRequest request, CancellationToken cancellationToken);

}

/// <summary>Sends shared DTOs through the common authenticated transport.</summary>
public sealed class StaffApi(ApiClient api) : IStaffApi
{
    /// <inheritdoc />
    public Task<ApiResult<List<BookingSummaryDto>>> ScheduleAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        api.SendAsync<List<BookingSummaryDto>>(HttpMethod.Get, $"api/v1/staff/me/bookings?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<BookingDetailDto>> CompleteAsync(int id, CompleteBookingRequest request, CancellationToken cancellationToken) =>
        api.SendJsonAsync<CompleteBookingRequest, BookingDetailDto>(HttpMethod.Put, $"api/v1/staff/bookings/{id}/complete", request, cancellationToken);

}
