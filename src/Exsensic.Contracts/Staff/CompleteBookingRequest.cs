using System.ComponentModel.DataAnnotations;

namespace Exsensic.Contracts.Staff;

/// <summary>
/// Body of PUT /api/v1/staff/bookings/{id}/complete, sent when the assigned staff member or an admin
/// marks a booking as delivered (docs/CONTRACTS.md §5). Completing before the slot starts gives 409 invalid_transition.
/// </summary>
/// <param name="RowVersion">The base64 concurrency token from the last read; a stale value gives 409 concurrency_conflict.</param>
public sealed record CompleteBookingRequest([Required] string RowVersion);
