using Exsensic.Contracts.Enums;

namespace Exsensic.Core.Bookings.States;

/// <summary>
/// A booking approved by an admin with staff assigned. It can be rescheduled (back to Requested for
/// re-approval), cancelled, or completed once the slot has started (docs/CONTRACTS.md §3).
/// </summary>
public sealed class ConfirmedState : BookingState
{
    /// <summary>The single shared instance; the state holds no data.</summary>
    public static ConfirmedState Instance { get; } = new();

    private ConfirmedState()
    {
    }

    /// <inheritdoc />
    public override BookingStatus Status => BookingStatus.Confirmed;

    /// <summary>
    /// Moving a confirmed booking to a new slot sends it back to Requested, because the admin must
    /// check that the assigned staff member is still free at the new time.
    /// </summary>
    public override BookingStatus Reschedule() => BookingStatus.Requested;

    /// <summary>
    /// The client or an admin cancels the booking: Confirmed → Cancelled. The client cut-off window is
    /// checked by the booking service before this is called, because admins may cancel at any time.
    /// </summary>
    public override BookingStatus Cancel() => BookingStatus.Cancelled;

    /// <summary>
    /// Marks the booking as delivered: Confirmed → Completed. Refused before the slot starts, because a
    /// service can't have been delivered yet.
    /// </summary>
    /// <param name="slotStartUtc">When the booked slot starts, in UTC.</param>
    /// <param name="nowUtc">The current time, in UTC, from the injected TimeProvider.</param>
    public override BookingStatus Complete(DateTimeOffset slotStartUtc, DateTimeOffset nowUtc)
    {
        if (nowUtc < slotStartUtc)
        {
            throw NotAllowed(BookingAction.Complete, "The booking can only be completed after its time slot has started.");
        }

        return BookingStatus.Completed;
    }
}
