using Exsensic.Contracts.Catalog;
using Exsensic.Web.Models.Journeys;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Exsensic.Web.Models.AdminCatalog;

/// <summary>
/// The time-slot list page for a date range.
/// </summary>
public sealed class AdminTimeSlotListViewModel : JourneyPage
{
  
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }

    [BindNever]
    public IReadOnlyList<TimeSlotDto> Slots { get; set; } = [];
}
