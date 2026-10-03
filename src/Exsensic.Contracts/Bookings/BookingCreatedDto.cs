using Exsensic.Contracts.Enums;

namespace Exsensic.Contracts.Bookings;

/// <summary>
/// Response of POST /api/v1/bookings (201 Created), so the Web can show the confirmation page
/// with the booking reference (docs/CONTRACTS.md §5).
/// </summary>
/// <param name="Id">The new booking's id.</param>
/// <param name="Reference">The human-readable booking reference shown to the client.</param>
/// <param name="Status">The starting status, always Requested.</param>
public sealed record BookingCreatedDto(int Id, string Reference, BookingStatus Status);
