using Exsensic.Contracts.Enums;
using Exsensic.Core.Bookings;
using Exsensic.Core.Bookings.States;
using Exsensic.Core.Exceptions;

namespace Exsensic.UnitTests.Bookings;

/// <summary>
/// Checks every cell of the transition table in docs/CONTRACTS.md §3 at the state level:
/// allowed cells return the right next status, forbidden cells throw InvalidBookingTransitionException.
/// </summary>
public class BookingStateTests
{
    private static readonly DateTimeOffset SlotStartUtc = new(2026, 10, 5, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AfterStartUtc = SlotStartUtc.AddMinutes(30);
    private static readonly DateTimeOffset BeforeStartUtc = SlotStartUtc.AddMinutes(-30);

    /// <summary>The allowed cells of the transition table: from status, action, expected next status.</summary>
    public static TheoryData<BookingStatus, BookingAction, BookingStatus> AllowedTransitions() => new()
    {
        { BookingStatus.Requested, BookingAction.Confirm, BookingStatus.Confirmed },
        { BookingStatus.Requested, BookingAction.Reject, BookingStatus.Cancelled },
        { BookingStatus.Requested, BookingAction.Reschedule, BookingStatus.Requested },
        { BookingStatus.Requested, BookingAction.Cancel, BookingStatus.Cancelled },
        { BookingStatus.Confirmed, BookingAction.Reschedule, BookingStatus.Requested },
        { BookingStatus.Confirmed, BookingAction.Cancel, BookingStatus.Cancelled },
        { BookingStatus.Confirmed, BookingAction.Complete, BookingStatus.Completed },
    };

    /// <summary>The forbidden (✗) cells of the transition table: from status and action.</summary>
    public static TheoryData<BookingStatus, BookingAction> ForbiddenTransitions() => new()
    {
        { BookingStatus.Requested, BookingAction.Complete },
        { BookingStatus.Confirmed, BookingAction.Confirm },
        { BookingStatus.Confirmed, BookingAction.Reject },
        { BookingStatus.Completed, BookingAction.Confirm },
        { BookingStatus.Completed, BookingAction.Reject },
        { BookingStatus.Completed, BookingAction.Reschedule },
        { BookingStatus.Completed, BookingAction.Cancel },
        { BookingStatus.Completed, BookingAction.Complete },
        { BookingStatus.Cancelled, BookingAction.Confirm },
        { BookingStatus.Cancelled, BookingAction.Reject },
        { BookingStatus.Cancelled, BookingAction.Reschedule },
        { BookingStatus.Cancelled, BookingAction.Cancel },
        { BookingStatus.Cancelled, BookingAction.Complete },
    };

    /// <summary>Each allowed action returns the next status from the contract table.</summary>
    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void Apply_AllowedTransition_ReturnsNextStatus(BookingStatus from, BookingAction action, BookingStatus expected)
    {
        var state = BookingState.For(from);

        var next = Apply(state, action, AfterStartUtc);

        Assert.Equal(expected, next);
    }

    /// <summary>Each forbidden action throws, naming the current status and the action.</summary>
    [Theory]
    [MemberData(nameof(ForbiddenTransitions))]
    public void Apply_ForbiddenTransition_ThrowsInvalidBookingTransition(BookingStatus from, BookingAction action)
    {
        var state = BookingState.For(from);

        var ex = Assert.Throws<InvalidBookingTransitionException>(() => Apply(state, action, AfterStartUtc));

        Assert.Equal(from, ex.CurrentStatus);
        Assert.Equal(action, ex.Action);
        Assert.Equal("invalid_transition", ex.Code);
    }

    /// <summary>A confirmed booking can't be completed before its slot starts.</summary>
    [Fact]
    public void Complete_ConfirmedBeforeSlotStart_ThrowsInvalidBookingTransition()
    {
        var ex = Assert.Throws<InvalidBookingTransitionException>(
            () => ConfirmedState.Instance.Complete(SlotStartUtc, BeforeStartUtc));

        Assert.Equal(BookingStatus.Confirmed, ex.CurrentStatus);
        Assert.Equal(BookingAction.Complete, ex.Action);
    }

    /// <summary>Completing exactly at the slot start is allowed.</summary>
    [Fact]
    public void Complete_ConfirmedAtSlotStart_ReturnsCompleted()
    {
        var next = ConfirmedState.Instance.Complete(SlotStartUtc, SlotStartUtc);

        Assert.Equal(BookingStatus.Completed, next);
    }

    /// <summary>Every contract status maps to a state that reports that same status.</summary>
    [Theory]
    [InlineData(BookingStatus.Requested)]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Completed)]
    [InlineData(BookingStatus.Cancelled)]
    public void For_KnownStatus_ReturnsMatchingState(BookingStatus status)
    {
        var state = BookingState.For(status);

        Assert.Equal(status, state.Status);
    }

    /// <summary>A value outside the four contract statuses is rejected instead of guessed.</summary>
    [Fact]
    public void For_UnknownStatus_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BookingState.For((BookingStatus)99));
    }

    /// <summary>Calls the state method that matches the action, so one theory can cover every column.</summary>
    private static BookingStatus Apply(BookingState state, BookingAction action, DateTimeOffset nowUtc) => action switch
    {
        BookingAction.Confirm => state.Confirm(),
        BookingAction.Reject => state.Reject(),
        BookingAction.Reschedule => state.Reschedule(),
        BookingAction.Cancel => state.Cancel(),
        BookingAction.Complete => state.Complete(SlotStartUtc, nowUtc),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Not a transition action."),
    };
}
