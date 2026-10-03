using Exsensic.Contracts.Enums;

namespace Exsensic.Core.Bookings.States;

/// <summary>
/// A booking waiting for admin approval. It can be confirmed, rejected, rescheduled or cancelled,
/// but not completed (docs/CONTRACTS.md §3).
/// </summary>
public sealed class RequestedState : BookingState
{
    /// <summary>The single shared instance; the state holds no data.</summary>
    public static RequestedState Instance { get; } = new();

    private RequestedState()
    {
    }

    /// <inheritdoc />
    public override BookingStatus Status => BookingStatus.Requested;

    /// <summary>An admin approves the booking: Requested → Confirmed.</summary>
    public override BookingStatus Confirm() => BookingStatus.Confirmed;

    /// <summary>An admin rejects the booking: Requested → Cancelled (the reason is stored by the booking).</summary>
    public override BookingStatus Reject() => BookingStatus.Cancelled;

    /// <summary>The booking moves to another slot and stays Requested, still waiting for approval.</summary>
    public override BookingStatus Reschedule() => BookingStatus.Requested;

    /// <summary>The client or an admin cancels the booking: Requested → Cancelled.</summary>
    public override BookingStatus Cancel() => BookingStatus.Cancelled;
}
