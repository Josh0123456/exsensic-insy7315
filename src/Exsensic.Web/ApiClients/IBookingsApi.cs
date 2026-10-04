using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Common;
using Exsensic.Contracts.Requirements;

namespace Exsensic.Web.ApiClients;

/// <summary>Typed access to the merged bookings API contracts; the API owns validation and authorization.</summary>
public interface IBookingsApi
{
    /// <summary>GET /api/v1/bookings/mine.</summary>
    Task<ApiResult<PagedResult<BookingSummaryDto>>> MineAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>GET /api/v1/bookings/{id}.</summary>
    Task<ApiResult<BookingDetailDto>> DetailAsync(int id, CancellationToken cancellationToken);

    /// <summary>GET /api/v1/services/{serviceId}/requirement-template.</summary>
    Task<ApiResult<RequirementTemplateDto>> TemplateAsync(int serviceId, CancellationToken cancellationToken);

    /// <summary>POST /api/v1/bookings.</summary>
    Task<ApiResult<BookingCreatedDto>> CreateAsync(CreateBookingRequest request, CancellationToken cancellationToken);

    /// <summary>PUT /api/v1/bookings/{id}/reschedule.</summary>
    Task<ApiResult<BookingDetailDto>> RescheduleAsync(int id, RescheduleBookingRequest request, CancellationToken cancellationToken);

    /// <summary>PUT /api/v1/bookings/{id}/cancel.</summary>
    Task<ApiResult<BookingDetailDto>> CancelAsync(int id, CancelBookingRequest request, CancellationToken cancellationToken);

}

/// <summary>Sends shared DTOs through the common authenticated transport.</summary>
public sealed class BookingsApi(ApiClient api) : IBookingsApi
{
    /// <inheritdoc />
    public Task<ApiResult<PagedResult<BookingSummaryDto>>> MineAsync(int page, int pageSize, CancellationToken cancellationToken) =>
        api.SendAsync<PagedResult<BookingSummaryDto>>(HttpMethod.Get, $"api/v1/bookings/mine?page={page}&pageSize={pageSize}", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<BookingDetailDto>> DetailAsync(int id, CancellationToken cancellationToken) =>
        api.SendAsync<BookingDetailDto>(HttpMethod.Get, $"api/v1/bookings/{id}", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<RequirementTemplateDto>> TemplateAsync(int serviceId, CancellationToken cancellationToken) =>
        api.SendAsync<RequirementTemplateDto>(HttpMethod.Get, $"api/v1/services/{serviceId}/requirement-template", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<BookingCreatedDto>> CreateAsync(CreateBookingRequest request, CancellationToken cancellationToken) =>
        api.SendJsonAsync<CreateBookingRequest, BookingCreatedDto>(HttpMethod.Post, "api/v1/bookings", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<BookingDetailDto>> RescheduleAsync(int id, RescheduleBookingRequest request, CancellationToken cancellationToken) =>
        api.SendJsonAsync<RescheduleBookingRequest, BookingDetailDto>(HttpMethod.Put, $"api/v1/bookings/{id}/reschedule", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<BookingDetailDto>> CancelAsync(int id, CancelBookingRequest request, CancellationToken cancellationToken) =>
        api.SendJsonAsync<CancelBookingRequest, BookingDetailDto>(HttpMethod.Put, $"api/v1/bookings/{id}/cancel", request, cancellationToken);

}
