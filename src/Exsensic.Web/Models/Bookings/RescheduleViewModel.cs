using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Bookings;

/// <summary>Reschedule form retaining the original concurrency token on errors.</summary>
public sealed class RescheduleViewModel : JourneyPage
{
    /// <summary>Id for the Web presentation.</summary>
    public Guid Id { get; set; }

    /// <summary>From for the Web presentation.</summary>
    public DateOnly From { get; set; }

    /// <summary>RowVersion for the Web presentation.</summary>
    [Required]
    public string RowVersion { get; set; } = "";

    /// <summary>NewTimeSlotId for the Web presentation.</summary>
    [Required(ErrorMessage = "Choose an available time.")]
    public string? NewTimeSlotId { get; set; }

    /// <summary>Detail for the Web presentation.</summary>
    [BindNever]
    public BookingDetailViewModel? Detail { get; set; }

    /// <summary>Picker for the Web presentation.</summary>
    [BindNever]
    public SlotPickerViewModel? Picker { get; set; }
}
