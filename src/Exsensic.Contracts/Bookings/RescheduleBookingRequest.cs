using System.ComponentModel.DataAnnotations;

namespace Exsensic.Contracts.Bookings;

/// <summary>
/// Body of PUT /api/v1/bookings/{id}/reschedule (docs/CONTRACTS.md §5).
/// A confirmed booking that is rescheduled goes back to Requested for re-approval.
/// </summary>
/// <param name="NewTimeSlotId">The time slot to move the booking to.</param>
/// <param name="RowVersion">The base64 concurrency token from the last read; a stale value gives 409 concurrency_conflict.</param>
public sealed record RescheduleBookingRequest(
    [Required] Guid NewTimeSlotId,
    [Required] string RowVersion);
