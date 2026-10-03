using Exsensic.Web.ApiClients;
using Exsensic.Web.Models.Services;

namespace Exsensic.Web.Journeys;

/// <summary>Web presentation adapter seam. No implementation is registered until the owning shared contracts/client exist.</summary>
public interface ICatalogJourney
{
    /// <summary>Maps P3's catalogue client output to the view; does not replace ICatalogApi.</summary>
    Task<ApiResult<ServiceListViewModel>> ListAsync(string? category, CancellationToken cancellationToken);

    /// <summary>Loads a real service through P3's catalogue client.</summary>
    Task<ApiResult<ServiceDetailViewModel>> DetailAsync(int id, CancellationToken cancellationToken);
}
