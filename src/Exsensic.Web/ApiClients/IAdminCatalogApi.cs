using Exsensic.Contracts.Catalog;

namespace Exsensic.Web.ApiClients;

/// <summary>
/// Typed client for the admin catalogue endpoints (services and time slots).
/// Every method returns an <see cref="ApiResult{T}"/> so the caller can show
/// either the value or a friendly error.
/// </summary>
public interface IAdminCatalogApi
{
    /// <summary>GET /api/v1/admin/services: all services, active or not.</summary>
    Task<ApiResult<List<ServiceDto>>> ListServicesAsync(CancellationToken ct);

    /// <summary>POST /api/v1/admin/services: creates a new service.</summary>
    Task<ApiResult<object>> CreateServiceAsync(SaveServiceRequest request, CancellationToken ct);

    /// <summary>PUT /api/v1/admin/services/{id}: updates a service.</summary>
    Task<ApiResult<object>> UpdateServiceAsync(int id, SaveServiceRequest request, CancellationToken ct);

    /// <summary>PUT /api/v1/admin/services/{id}/active: archives or restores a service.</summary>
    Task<ApiResult<object>> SetServiceActiveAsync(int id, SetActiveRequest request, CancellationToken ct);

    /// <summary>GET /api/v1/admin/timeslots: slots in a date range.</summary>
    Task<ApiResult<List<TimeSlotDto>>> ListTimeSlotsAsync(DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>POST /api/v1/admin/timeslots/generate: creates slots over a date range.</summary>
    Task<ApiResult<GenerateSlotsResult>> GenerateSlotsAsync(GenerateSlotsRequest request, CancellationToken ct);

    /// <summary>PUT /api/v1/admin/timeslots/{id}/block: blocks a slot with a reason.</summary>
    Task<ApiResult<object>> BlockSlotAsync(int id, BlockSlotRequest request, CancellationToken ct);

    /// <summary>PUT /api/v1/admin/timeslots/{id}/unblock: removes a block.</summary>
    Task<ApiResult<object>> UnblockSlotAsync(int id, CancellationToken ct);

    /// <summary>DELETE /api/v1/admin/timeslots/{id}: deletes a slot with no active booking.</summary>
    Task<ApiResult<object>> DeleteSlotAsync(int id, CancellationToken ct);
}
