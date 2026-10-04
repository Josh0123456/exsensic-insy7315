using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Admin;

/// <summary>Admin table/card presentation for one API-backed booking.</summary>
public sealed class AdminBookingRowViewModel
{
    /// <summary>Booking for the Web presentation.</summary>
    public BookingCardViewModel Booking { get; set; } = null!;

    /// <summary>ClientName for the Web presentation.</summary>
    public string? ClientName { get; set; }
}
