using Exsensic.Contracts.Enums;

namespace Exsensic.Core.Bookings;

/// <summary>
/// A domain event recorded every time a booking action succeeds, including the first one when the
/// booking is created. The Observer pattern (status history and in-app notifications) reacts to these
/// events, so the booking itself never has to know who is listening.
/// </summary>
/// <param name="BookingId">The booking that changed.</param>
/// <param name="Action">What was done to the booking.</param>
/// <param name="FromStatus">The status before the action, or null when the booking was just created.</param>
/// <param name="ToStatus">The status after the action.</param>
/// <param name="ChangedByUserId">The user who performed the action.</param>
/// <param name="ChangedAtUtc">When the action happened, in UTC, taken from the injected TimeProvider.</param>
/// <param name="Note">An optional note, for example the rejection or cancellation reason.</param>
public sealed record BookingStatusChanged(
    int BookingId,
    BookingAction Action,
    BookingStatus? FromStatus,
    BookingStatus ToStatus,
    Guid ChangedByUserId,
    DateTimeOffset ChangedAtUtc,
    string? Note);
