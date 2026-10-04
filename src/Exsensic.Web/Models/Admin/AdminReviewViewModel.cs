using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;
using Exsensic.Web.Models.Bookings;

namespace Exsensic.Web.Models.Admin;

/// <summary>Review page composed from booking details and API-supplied staff options.</summary>
public sealed class AdminReviewViewModel : JourneyPage
{
    /// <summary>Id for the Web presentation.</summary>
    public int Id { get; set; }

    /// <summary>Detail for the Web presentation.</summary>
    [BindNever]
    public BookingDetailViewModel? Detail { get; set; }

    /// <summary>StaffOptions for the Web presentation.</summary>
    [BindNever]
    public IReadOnlyList<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> StaffOptions { get; set; } = [];

    /// <summary>Confirm for the Web presentation.</summary>
    public ConfirmBookingViewModel Confirm { get; set; } = new();

    /// <summary>Reject for the Web presentation.</summary>
    public RejectBookingViewModel Reject { get; set; } = new();
}
