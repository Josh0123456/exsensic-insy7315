using Exsensic.Contracts.Enums;

namespace Exsensic.Core.Bookings.States;

/// <summary>
/// A booking whose service has been delivered. This is a final status: it overrides nothing, so every
/// action is refused by the base class (docs/CONTRACTS.md §3).
/// </summary>
public sealed class CompletedState : BookingState
{
    /// <summary>The single shared instance; the state holds no data.</summary>
    public static CompletedState Instance { get; } = new();

    private CompletedState()
    {
    }

    /// <inheritdoc />
    public override BookingStatus Status => BookingStatus.Completed;
}
