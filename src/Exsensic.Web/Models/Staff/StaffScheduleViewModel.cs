using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Staff;

/// <summary>Weekly staff workspace with entries grouped by their supplied booking dates.</summary>
public sealed class StaffScheduleViewModel : JourneyPage
{
    /// <summary>WeekStart for the Web presentation.</summary>
    public DateOnly WeekStart { get; set; }

    /// <summary>Today for the Web presentation.</summary>
    [BindNever]
    public DateOnly Today { get; set; }

    /// <summary>Entries for the Web presentation.</summary>
    [BindNever]
    public IReadOnlyList<StaffScheduleItemViewModel> Entries { get; set; } = [];
}
