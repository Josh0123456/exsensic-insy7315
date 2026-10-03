using Exsensic.Core.Entities;

namespace Exsensic.Core.Bookings.Observers;

/// <summary>
/// Hands every event a booking has recorded to every registered observer, in registration order
/// (status history first, then notifications). The booking services call it after a domain action
/// and before SaveChanges, so the booking change, its history and its notifications are saved in
/// one transaction: either all of them are stored or none are.
/// </summary>
/// <remarks>
/// For a new booking, call it after the first SaveChanges inside the transaction, once the booking
/// has its database id, because notifications link to the booking by id.
/// </remarks>
public sealed class BookingEventDispatcher
{
    private readonly IEnumerable<IBookingObserver> _observers;

    /// <summary>
    /// Creates the dispatcher with every observer registered in dependency injection.
    /// </summary>
    /// <param name="observers">The observers, in the order they were registered.</param>
    public BookingEventDispatcher(IEnumerable<IBookingObserver> observers)
    {
        _observers = observers;
    }

    /// <summary>
    /// Sends the booking's recorded events to all observers, then clears them so they are never handled twice.
    /// </summary>
    /// <param name="booking">The booking whose events should be handled.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    public async Task DispatchAsync(Booking booking, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(booking);

        foreach (var change in booking.StatusChanges)
        {
            foreach (var observer in _observers)
            {
                await observer.OnStatusChangedAsync(booking, change, ct);
            }
        }

        booking.ClearStatusChanges();
    }
}
