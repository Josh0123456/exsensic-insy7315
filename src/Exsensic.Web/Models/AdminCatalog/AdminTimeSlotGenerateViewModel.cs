using System.ComponentModel.DataAnnotations;
using Exsensic.Web.Models.Journeys;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Exsensic.Web.Models.AdminCatalog;

/// <summary>
/// The generate-slots form.
/// </summary>
public sealed class AdminTimeSlotGenerateViewModel : JourneyPage
{
   
    [DataType(DataType.Date), Display(Name = "From")]
    public DateOnly FromDate { get; set; }

    [DataType(DataType.Date), Display(Name = "To")]
    public DateOnly ToDate { get; set; }

    [Display(Name = "Weekdays")]
    public DayOfWeek[] Weekdays { get; set; } = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    /// <summary>Start times as a comma-separated string, e.g. "09:00, 12:00, 14:00".</summary>
    [Required, Display(Name = "Start times (comma separated)")]
    public string StartTimesText { get; set; } = "09:00, 12:00, 14:00";

    ///How long each slot lasts.
    [Range(15, 480), Display(Name = "Duration (minutes)")]
    public int DurationMinutes { get; set; } = 60;

    [BindNever]
    public string? ResultSummary { get; set; }
}
