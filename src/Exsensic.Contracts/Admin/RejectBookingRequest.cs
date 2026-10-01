using System.ComponentModel.DataAnnotations;

namespace Exsensic.Contracts.Admin;

/// <summary>
/// Body of PUT /api/v1/admin/bookings/{id}/reject (docs/CONTRACTS.md §5).
/// A rejection moves the booking to Cancelled and stores the reason with the "Rejected: " prefix.
/// </summary>
/// <param name="Reason">Why the booking was rejected. Required, so the client always gets an explanation.</param>
/// <param name="RowVersion">The base64 concurrency token from the last read; a stale value gives 409 concurrency_conflict.</param>
public sealed record RejectBookingRequest(
    [Required, StringLength(500)] string Reason,
    [Required] string RowVersion);
