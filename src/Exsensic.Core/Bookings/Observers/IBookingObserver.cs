using Exsensic.Core.Entities;

namespace Exsensic.Core.Bookings.Observers;

/// <summary>
/// Something that reacts when a booking's status changes: the Observer half of the Observer pattern.
/// The booking only records what happened; each observer decides what to do about it (write the
/// status history, notify people), so a new reaction is a new observer and the booking never changes.
/// </summary>
// Adapted from [2]: Refactoring.Guru (n.d.) Observer. https://refactoring.guru/design-patterns/observer
public interface IBookingObserver
{
    /// <summary>
    /// Handles one status-change event. Observers only add rows to the context; they never call
    /// SaveChanges, so everything is saved together with the booking change.
    /// </summary>
    /// <param name="booking">The booking that changed, tracked by the database context.</param>
    /// <param name="change">The event the booking recorded.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    Task OnStatusChangedAsync(Booking booking, BookingStatusChanged change, CancellationToken ct);
}
