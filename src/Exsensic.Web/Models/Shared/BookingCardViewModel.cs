namespace Exsensic.Web.Models.Shared;

/// <summary>Display-only booking summary with caller-selected actions.</summary>
public sealed class BookingCardViewModel
{
    /// <summary>Human-readable booking reference.</summary>
    public required string Reference { get; init; }

    /// <summary>Booked service name.</summary>
    public required string ServiceName { get; init; }

    /// <summary>Booking date already converted to the display time zone.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Optional local start time.</summary>
    public TimeOnly? StartTime { get; init; }

    /// <summary>Optional local end time.</summary>
    public TimeOnly? EndTime { get; init; }

    /// <summary>Contract status name; no state transitions are decided here.</summary>
    public required string BookingStatus { get; init; }

    /// <summary>Optional assigned staff display name.</summary>
    public string? AssignedStaff { get; init; }

    /// <summary>Local booking details destination.</summary>
    public required string DetailsUrl { get; init; }

    /// <summary>Optional primary action label.</summary>
    public string? ActionText { get; init; }

    /// <summary>Optional local action page; never a mutation endpoint.</summary>
    public string? ActionUrl { get; init; }
}
