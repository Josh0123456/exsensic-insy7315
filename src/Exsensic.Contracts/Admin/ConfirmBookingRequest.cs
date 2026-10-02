using System.ComponentModel.DataAnnotations;

namespace Exsensic.Contracts.Admin;

/// <summary>
/// Body of PUT /api/v1/admin/bookings/{id}/confirm, sent when an admin approves a booking and
/// assigns a staff member (docs/CONTRACTS.md §5). The API re-checks that the staff member is
/// qualified and free, else 409 staff_unavailable.
/// </summary>
/// <param name="StaffUserId">The staff member to assign, chosen from GET /api/v1/admin/staff.</param>
/// <param name="RowVersion">The base64 concurrency token from the last read; a stale value gives 409 concurrency_conflict.</param>
public sealed record ConfirmBookingRequest(
    [Required] Guid StaffUserId,
    [Required] string RowVersion);
