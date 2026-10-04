using Exsensic.Contracts.Catalog;
using Exsensic.Web.Models.Journeys;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Exsensic.Web.Models.AdminCatalog;

/// <summary>
/// The service list page: every service with its state.
/// </summary>
public sealed class AdminServiceListViewModel : JourneyPage
{
   
    [BindNever]
    public IReadOnlyList<ServiceDto> Services { get; set; } = [];
}
