using System.ComponentModel.DataAnnotations;

namespace Exsensic.Contracts.Bookings;

/// <summary>
/// Body of PUT /api/v1/bookings/{id}/cancel (docs/CONTRACTS.md §5).
/// Clients can't cancel inside the cut-off window (409 cancel_window_closed); admins can at any time.
/// </summary>
/// <param name="Reason">An optional reason for cancelling, kept on the booking.</param>
/// <param name="RowVersion">The base64 concurrency token from the last read; a stale value gives 409 concurrency_conflict.</param>
public sealed record CancelBookingRequest(
    [StringLength(500)] string? Reason,
    [Required] string RowVersion);
