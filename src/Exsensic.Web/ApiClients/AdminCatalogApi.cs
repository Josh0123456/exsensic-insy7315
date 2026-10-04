using Exsensic.Contracts.Catalog;

namespace Exsensic.Web.ApiClients;

/// <summary>
/// The <see cref="IAdminCatalogApi"/> implementation over the shared JSON transport.
/// Paths match docs/CONTRACTS.md §6 exactly. No business rules here.
/// </summary>
public sealed class AdminCatalogApi(ApiClient api) : IAdminCatalogApi
{

    public Task<ApiResult<List<ServiceDto>>> ListServicesAsync(CancellationToken ct) =>
        api.SendAsync<List<ServiceDto>>(HttpMethod.Get, "api/v1/admin/services", ct);

    public Task<ApiResult<object>> CreateServiceAsync(SaveServiceRequest request, CancellationToken ct) =>
        api.SendJsonNoContentAsync(HttpMethod.Post, "api/v1/admin/services", request, ct);

    public Task<ApiResult<object>> UpdateServiceAsync(int id, SaveServiceRequest request, CancellationToken ct) =>
        api.SendJsonNoContentAsync(HttpMethod.Put, $"api/v1/admin/services/{id}", request, ct);

    public Task<ApiResult<object>> SetServiceActiveAsync(int id, SetActiveRequest request, CancellationToken ct) =>
        api.SendJsonNoContentAsync(HttpMethod.Put, $"api/v1/admin/services/{id}/active", request, ct);

    public Task<ApiResult<List<TimeSlotDto>>> ListTimeSlotsAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        api.SendAsync<List<TimeSlotDto>>(
            HttpMethod.Get,
            $"api/v1/admin/timeslots?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            ct);


    public Task<ApiResult<GenerateSlotsResult>> GenerateSlotsAsync(GenerateSlotsRequest request, CancellationToken ct) =>
        api.SendJsonAsync<GenerateSlotsRequest, GenerateSlotsResult>(
            HttpMethod.Post, "api/v1/admin/timeslots/generate", request, ct);

    public Task<ApiResult<object>> BlockSlotAsync(int id, BlockSlotRequest request, CancellationToken ct) =>
        api.SendJsonNoContentAsync(HttpMethod.Put, $"api/v1/admin/timeslots/{id}/block", request, ct);

    public Task<ApiResult<object>> UnblockSlotAsync(int id, CancellationToken ct) =>
        api.SendNoContentAsync(HttpMethod.Put, $"api/v1/admin/timeslots/{id}/unblock", ct);


    public Task<ApiResult<object>> DeleteSlotAsync(int id, CancellationToken ct) =>
        api.SendNoContentAsync(HttpMethod.Delete, $"api/v1/admin/timeslots/{id}", ct);
}
