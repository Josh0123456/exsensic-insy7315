using Exsensic.Contracts.Enums;

namespace Exsensic.Core.Bookings.States;

/// <summary>
/// A booking that was cancelled or rejected. This is a final status: it overrides nothing, so every
/// action is refused by the base class (docs/CONTRACTS.md §3).
/// </summary>
public sealed class CancelledState : BookingState
{
    /// <summary>The single shared instance; the state holds no data.</summary>
    public static CancelledState Instance { get; } = new();

    private CancelledState()
    {
    }

    /// <inheritdoc />
    public override BookingStatus Status => BookingStatus.Cancelled;
}
