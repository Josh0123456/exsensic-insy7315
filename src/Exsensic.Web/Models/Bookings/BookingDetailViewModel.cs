using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Bookings;

/// <summary>Booking detail composition and API-backed action affordances.</summary>
public sealed class BookingDetailViewModel : JourneyPage
{
    /// <summary>Id for the Web presentation.</summary>
    public Guid Id { get; set; }

    /// <summary>ServiceId for the Web presentation.</summary>
    [BindNever]
    public int ServiceId { get; set; }

    /// <summary>TimeSlotId for the Web presentation.</summary>
    [BindNever]
    public Guid TimeSlotId { get; set; }

    /// <summary>Booking for the Web presentation.</summary>
    [BindNever]
    public BookingCardViewModel? Booking { get; set; }

    /// <summary>RowVersion for the Web presentation.</summary>
    [BindNever]
    public string? RowVersion { get; set; }

    /// <summary>ClientCompany for the Web presentation.</summary>
    [BindNever]
    public string? ClientCompany { get; set; }

    /// <summary>ContactSummary for the Web presentation.</summary>
    [BindNever]
    public string? ContactSummary { get; set; }

    /// <summary>CancellationReason for the Web presentation.</summary>
    [BindNever]
    public string? CancellationReason { get; set; }

    /// <summary>Requirements for the Web presentation.</summary>
    [BindNever]
    public IReadOnlyList<KeyValuePair<string,string>> Requirements { get; set; } = [];

    /// <summary>History for the Web presentation.</summary>
    [BindNever]
    public IReadOnlyList<BookingHistoryViewModel> History { get; set; } = [];

    /// <summary>CanReschedule for the Web presentation.</summary>
    [BindNever]
    public bool CanReschedule { get; set; }

    /// <summary>CanCancel for the Web presentation.</summary>
    [BindNever]
    public bool CanCancel { get; set; }

    /// <summary>CanComplete for the Web presentation.</summary>
    [BindNever]
    public bool CanComplete { get; set; }

    /// <summary>CanReview for the Web presentation.</summary>
    [BindNever]
    public bool CanReview { get; set; }
}
