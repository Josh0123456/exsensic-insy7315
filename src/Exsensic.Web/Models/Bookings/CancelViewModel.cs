using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Bookings;

/// <summary>Cancellation confirmation form; the API enforces the cut-off.</summary>
public sealed class CancelViewModel : JourneyPage
{
    /// <summary>Id for the Web presentation.</summary>
    public int Id { get; set; }

    /// <summary>RowVersion for the Web presentation.</summary>
    [Required]
    public string RowVersion { get; set; } = "";

    /// <summary>Reason for the Web presentation.</summary>
    [Display(Name = "Reason (optional)")]
    public string? Reason { get; set; }

    /// <summary>Detail for the Web presentation.</summary>
    [BindNever]
    public BookingDetailViewModel? Detail { get; set; }

    /// <summary>CancellationCutoffHours for the Web presentation.</summary>
    [BindNever]
    public int? CancellationCutoffHours { get; set; }
}
