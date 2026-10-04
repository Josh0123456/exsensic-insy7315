using System.ComponentModel.DataAnnotations;

namespace Exsensic.Contracts.Bookings;

/// <summary>
/// Body of POST /api/v1/bookings, sent when a client finishes the booking wizard (docs/CONTRACTS.md §5).
/// These annotations only check the shape; the API checks the requirement answers against the
/// service category's template and returns 400 validation_failed for unknown or invalid keys.
/// </summary>
/// <param name="ServiceId">The service being booked.</param>
/// <param name="TimeSlotId">The time slot the client picked.</param>
/// <param name="Requirements">The client's answers, keyed by requirement field key.</param>
public sealed record CreateBookingRequest(
    [Range(1, int.MaxValue)] int ServiceId,
    [Range(1, int.MaxValue)] int TimeSlotId,
    [Required] Dictionary<string, string> Requirements);
