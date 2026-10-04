using Exsensic.Contracts.Catalog;

namespace Exsensic.Web.ApiClients;

/// <summary>
/// Typed client for the admin catalogue endpoints (services and time slots).
/// Every method returns an <see cref="ApiResult{T}"/> so the caller can show
/// either the value or a friendly error.
/// </summary>
public interface IAdminCatalogApi
{
    Task<ApiResult<List<ServiceDto>>> ListServicesAsync(CancellationToken ct);

  
    Task<ApiResult<object>> CreateServiceAsync(SaveServiceRequest request, CancellationToken ct);

    Task<ApiResult<object>> UpdateServiceAsync(int id, SaveServiceRequest request, CancellationToken ct);

    Task<ApiResult<object>> SetServiceActiveAsync(int id, SetActiveRequest request, CancellationToken ct);

    Task<ApiResult<List<TimeSlotDto>>> ListTimeSlotsAsync(DateOnly from, DateOnly to, CancellationToken ct);

    Task<ApiResult<GenerateSlotsResult>> GenerateSlotsAsync(GenerateSlotsRequest request, CancellationToken ct);


    Task<ApiResult<object>> UnblockSlotAsync(int id, CancellationToken ct);

    Task<ApiResult<object>> DeleteSlotAsync(int id, CancellationToken ct);
}
