using Exsensic.Contracts.Catalog;
using Exsensic.Web.ApiClients;
using Exsensic.Web.Models.Services;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Journeys;

/// <summary>
/// Connects the public service screens to the catalogue API (docs/CONTRACTS.md §6):
/// GET /api/v1/services and GET /api/v1/services/{id}. Everything shown comes from the API.
/// </summary>
public sealed class CatalogJourney(ApiClient api) : ICatalogJourney
{
    /// <summary>Loads active services; the optional category filter is applied to what the API returned.</summary>
    public async Task<ApiResult<ServiceListViewModel>> ListAsync(string? category, CancellationToken cancellationToken)
    {
        var result = await api.SendAsync<List<ServiceDto>>(HttpMethod.Get, "api/v1/services", cancellationToken);
        if (result.Value is not { } services)
        {
            return new ApiResult<ServiceListViewModel>(result.StatusCode, default, false, result.Error);
        }

        var model = new ServiceListViewModel
        {
            Category = category,
            Categories = services.Select(s => s.Category).Distinct().ToList(),
            Services = services
                .Where(s => string.IsNullOrEmpty(category) || s.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .Select(ToCard)
                .ToList(),
            IntegrationAvailable = true,
        };
        return new ApiResult<ServiceListViewModel>(result.StatusCode, model, true, null);
    }

    /// <summary>Loads one active service; a missing one comes back as the API's 404.</summary>
    public async Task<ApiResult<ServiceDetailViewModel>> DetailAsync(int id, CancellationToken cancellationToken)
    {
        var result = await api.SendAsync<ServiceDto>(HttpMethod.Get, $"api/v1/services/{id}", cancellationToken);
        if (result.Value is not { } service)
        {
            return new ApiResult<ServiceDetailViewModel>(result.StatusCode, default, false, result.Error);
        }

        var model = new ServiceDetailViewModel { Service = ToCard(service), IntegrationAvailable = true };
        return new ApiResult<ServiceDetailViewModel>(result.StatusCode, model, true, null);
    }

    private static ServiceCardViewModel ToCard(ServiceDto s) => new()
    {
        ServiceId = s.Id.ToString(),
        Name = s.Name,
        Category = s.Category,
        Description = s.Description,
        DurationMinutes = s.DurationMinutes,
        StartingPrice = s.BasePrice,
        DetailsUrl = $"/Services/{s.Id}",
        BookingUrl = $"/Book/{s.Id}",
    };
}
