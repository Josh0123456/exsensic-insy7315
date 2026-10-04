using Exsensic.Contracts.Enums;
using Exsensic.Core.Exceptions;

namespace Exsensic.Core.Bookings.States;

/// <summary>
/// The base of the booking State pattern. Each status has one subclass that decides which actions it
/// allows and which status each action leads to (the transition table in docs/CONTRACTS.md §3).
/// Every action throws <see cref="InvalidBookingTransitionException"/> by default, so a status only
/// allows what its subclass explicitly overrides, and a forgotten case fails safely instead of slipping through.
/// </summary>
/// <remarks>
/// The states only answer "what is the next status?". Booking.Behaviour.cs applies the answer to the
/// booking and records the BookingStatusChanged event, so the status is changed in exactly one place.
/// States hold no data, so one shared instance per status is enough.
/// </remarks>
// Adapted from [1]: Refactoring.Guru (n.d.) State. https://refactoring.guru/design-patterns/state
public abstract class BookingState
{
    /// <summary>The status this state represents.</summary>
    public abstract BookingStatus Status { get; }

    /// <summary>
    /// Returns the state object for a status, so a booking loaded from the database gets the right behaviour.
    /// </summary>
    /// <param name="status">The booking's current status.</param>
    /// <returns>The shared state instance for that status.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The status is not one of the four contract values.</exception>
    public static BookingState For(BookingStatus status) => status switch
    {
        BookingStatus.Requested => RequestedState.Instance,
        BookingStatus.Confirmed => ConfirmedState.Instance,
        BookingStatus.Completed => CompletedState.Instance,
        BookingStatus.Cancelled => CancelledState.Instance,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown booking status."),
    };

    /// <summary>An admin approves the booking. Returns the next status, or throws if not allowed.</summary>
    public virtual BookingStatus Confirm() => throw NotAllowed(BookingAction.Confirm);

    /// <summary>An admin rejects the booking. Returns the next status, or throws if not allowed.</summary>
    public virtual BookingStatus Reject() => throw NotAllowed(BookingAction.Reject);

    /// <summary>The client or an admin moves the booking to another slot. Returns the next status, or throws if not allowed.</summary>
    public virtual BookingStatus Reschedule() => throw NotAllowed(BookingAction.Reschedule);

    /// <summary>The client or an admin cancels the booking. Returns the next status, or throws if not allowed.</summary>
    public virtual BookingStatus Cancel() => throw NotAllowed(BookingAction.Cancel);

    /// <summary>
    /// The assigned staff member or an admin marks the booking as delivered. Returns the next status, or throws if not allowed.
    /// </summary>
    /// <param name="slotStartUtc">When the booked slot starts, in UTC.</param>
    /// <param name="nowUtc">The current time, in UTC, from the injected TimeProvider.</param>
    public virtual BookingStatus Complete(DateTimeOffset slotStartUtc, DateTimeOffset nowUtc)
        => throw NotAllowed(BookingAction.Complete);

    /// <summary>
    /// Builds the exception for an action this status doesn't allow. Subclasses use it too when an
    /// allowed action fails a timing rule, so every refusal looks the same to the API.
    /// </summary>
    /// <param name="action">The action that was attempted.</param>
    /// <param name="detail">An optional extra explanation.</param>
    protected InvalidBookingTransitionException NotAllowed(BookingAction action, string? detail = null)
        => new(Status, action, detail);
}
