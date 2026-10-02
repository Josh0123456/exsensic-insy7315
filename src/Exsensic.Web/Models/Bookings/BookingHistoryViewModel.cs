using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Bookings;

/// <summary>A display-only history entry with no internal user identifier.</summary>
public sealed class BookingHistoryViewModel
{
    /// <summary>Status for the Web presentation.</summary>
    public string Status { get; set; } = "";

    /// <summary>ChangedAtUtc for the Web presentation.</summary>
    public DateTimeOffset ChangedAtUtc { get; set; }

    /// <summary>ChangedByName for the Web presentation.</summary>
    public string? ChangedByName { get; set; }

    /// <summary>Note for the Web presentation.</summary>
    public string? Note { get; set; }
}
