using Exsensic.Core.Entities;

namespace Exsensic.Core.Bookings.Observers;

/// <summary>
/// Writes one BookingStatusHistory row for every status-change event, so each booking has a complete
/// audit trail of who changed it, when, and why. The client's booking timeline is built from these rows.
/// </summary>
public sealed class StatusHistoryObserver : IBookingObserver
{
    /// <summary>
    /// Adds the history row through the booking's StatusHistory collection, so EF Core fills in the
    /// booking id when it saves, even for a booking that is new in this transaction.
    /// </summary>
    /// <param name="booking">The booking that changed.</param>
    /// <param name="change">The event to record.</param>
    /// <param name="ct">Not used; the row is only added to the context here.</param>
    public Task OnStatusChangedAsync(Booking booking, BookingStatusChanged change, CancellationToken ct)
    {
        booking.StatusHistory.Add(new BookingStatusHistory
        {
            FromStatus = change.FromStatus,
            ToStatus = change.ToStatus,
            ChangedByUserId = change.ChangedByUserId,
            ChangedAtUtc = change.ChangedAtUtc,
            Note = change.Note,
        });

        return Task.CompletedTask;
    }
}
