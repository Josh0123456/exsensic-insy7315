using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Staff;

/// <summary>Staff-specific presentation pairing a booking card with company text.</summary>
public sealed class StaffScheduleItemViewModel
{
    /// <summary>Booking for the Web presentation.</summary>
    public BookingCardViewModel Booking { get; set; } = null!;

    /// <summary>ClientCompany for the Web presentation.</summary>
    public string? ClientCompany { get; set; }
}
