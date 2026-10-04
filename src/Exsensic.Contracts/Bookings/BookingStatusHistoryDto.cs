using Exsensic.Contracts.Enums;

namespace Exsensic.Contracts.Bookings;

/// <summary>
/// One step in a booking's status timeline (docs/CONTRACTS.md §5).
/// It names who made the change but carries no internal user id, so ids never leave the API here.
/// </summary>
/// <param name="FromStatus">The status before the change, or null for the first entry when the booking was created.</param>
/// <param name="ToStatus">The status after the change.</param>
/// <param name="ChangedAtUtc">When the change happened, in UTC.</param>
/// <param name="ChangedByName">The full name of the person who made the change, or null if unknown.</param>
/// <param name="Note">An optional note, for example the rejection or cancellation reason.</param>
public sealed record BookingStatusHistoryDto(
    BookingStatus? FromStatus,
    BookingStatus ToStatus,
    DateTimeOffset ChangedAtUtc,
    string? ChangedByName,
    string? Note);
