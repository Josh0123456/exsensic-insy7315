using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Book;

/// <summary>First wizard step and its availability window.</summary>
public sealed class SlotStepViewModel : JourneyPage
{
    /// <summary>ServiceId for the Web presentation.</summary>
    public Guid ServiceId { get; set; }

    /// <summary>From for the Web presentation.</summary>
    public DateOnly From { get; set; }

    /// <summary>TimeSlotId for the Web presentation.</summary>
    [Required(ErrorMessage = "Choose an available time.")]
    public string? TimeSlotId { get; set; }

    /// <summary>Service for the Web presentation.</summary>
    [BindNever]
    public ServiceCardViewModel? Service { get; set; }

    /// <summary>Picker for the Web presentation.</summary>
    [BindNever]
    public SlotPickerViewModel? Picker { get; set; }
}
