using Exsensic.Contracts.Enums;
using Exsensic.Core.Bookings;
using Exsensic.Core.Entities;
using Exsensic.Core.Exceptions;

namespace Exsensic.UnitTests.Bookings;

/// <summary>
/// Tests every cell of the transition table in docs/CONTRACTS.md §3 through the Booking entity itself:
/// allowed cells change the status and record exactly one event; forbidden cells throw
/// InvalidBookingTransitionException and leave the booking untouched. Extra facts cover the special rules.
/// </summary>
public class BookingTransitionMatrixTests
{
    private static readonly Guid ClientId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AdminId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StaffId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherStaffId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly DateTimeOffset CreatedUtc = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SlotStartUtc = new(2026, 10, 5, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ActionUtc = SlotStartUtc.AddHours(1);

    /// <summary>The allowed cells: from status, action, expected status.</summary>
    public static TheoryData<BookingStatus, BookingAction, BookingStatus> AllowedCells() => new()
    {
        { BookingStatus.Requested, BookingAction.Confirm, BookingStatus.Confirmed },
        { BookingStatus.Requested, BookingAction.Reject, BookingStatus.Cancelled },
        { BookingStatus.Requested, BookingAction.Reschedule, BookingStatus.Requested },
        { BookingStatus.Requested, BookingAction.Cancel, BookingStatus.Cancelled },
        { BookingStatus.Confirmed, BookingAction.Reschedule, BookingStatus.Requested },
        { BookingStatus.Confirmed, BookingAction.Cancel, BookingStatus.Cancelled },
        { BookingStatus.Confirmed, BookingAction.Complete, BookingStatus.Completed },
    };

    /// <summary>The forbidden (✗) cells: from status and action.</summary>
    public static TheoryData<BookingStatus, BookingAction> ForbiddenCells() => new()
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

    /// <summary>An allowed action sets the expected status and records exactly one matching event.</summary>
    [Theory]
    [MemberData(nameof(AllowedCells))]
    public void Act_AllowedCell_ChangesStatusAndRecordsOneEvent(BookingStatus from, BookingAction action, BookingStatus expected)
    {
        var booking = BookingIn(from);
        var eventsBefore = booking.StatusChanges.Count;

        Act(booking, action);

        Assert.Equal(expected, booking.Status);
        Assert.Equal(eventsBefore + 1, booking.StatusChanges.Count);

        var change = booking.StatusChanges[^1];
        Assert.Equal(action, change.Action);
        Assert.Equal(from, change.FromStatus);
        Assert.Equal(expected, change.ToStatus);
        Assert.Equal(ActionUtc, change.ChangedAtUtc);
        Assert.Equal(ActionUtc, booking.UpdatedAtUtc);
    }

    /// <summary>A forbidden action throws and leaves the status, timestamp and events unchanged.</summary>
    [Theory]
    [MemberData(nameof(ForbiddenCells))]
    public void Act_ForbiddenCell_ThrowsAndLeavesBookingUnchanged(BookingStatus from, BookingAction action)
    {
        var booking = BookingIn(from);
        var eventsBefore = booking.StatusChanges.Count;
        var updatedBefore = booking.UpdatedAtUtc;
        var slotBefore = booking.TimeSlotId;
        var staffBefore = booking.StaffUserId;
        var reasonBefore = booking.CancellationReason;

        var ex = Assert.Throws<InvalidBookingTransitionException>(() => Act(booking, action));

        Assert.Equal(from, ex.CurrentStatus);
        Assert.Equal(from, booking.Status);
        Assert.Equal(eventsBefore, booking.StatusChanges.Count);
        Assert.Equal(updatedBefore, booking.UpdatedAtUtc);
        Assert.Equal(slotBefore, booking.TimeSlotId);
        Assert.Equal(staffBefore, booking.StaffUserId);
        Assert.Equal(reasonBefore, booking.CancellationReason);
    }

    /// <summary>A new booking starts Requested with one Create event that has no previous status.</summary>
    [Fact]
    public void Create_NewBooking_IsRequestedWithCreateEvent()
    {
        var booking = NewBooking();

        Assert.Equal(BookingStatus.Requested, booking.Status);
        Assert.Equal(CreatedUtc, booking.CreatedAtUtc);
        var change = Assert.Single(booking.StatusChanges);
        Assert.Equal(BookingAction.Create, change.Action);
        Assert.Null(change.FromStatus);
        Assert.Equal(ClientId, change.ChangedByUserId);
    }

    /// <summary>Confirm assigns the chosen staff member.</summary>
    [Fact]
    public void Confirm_Requested_AssignsStaff()
    {
        var booking = NewBooking();

        booking.Confirm(StaffId, AdminId, ActionUtc);

        Assert.Equal(StaffId, booking.StaffUserId);
        Assert.Equal(AdminId, booking.StatusChanges[^1].ChangedByUserId);
    }

    /// <summary>Rejecting without a reason fails and changes nothing.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_WithoutReason_ThrowsAndLeavesBookingRequested(string reason)
    {
        var booking = NewBooking();

        Assert.Throws<ArgumentException>(() => booking.Reject(reason, AdminId, ActionUtc));

        Assert.Equal(BookingStatus.Requested, booking.Status);
        Assert.Null(booking.CancellationReason);
        Assert.Single(booking.StatusChanges);
    }

    /// <summary>Reject stores the reason with the "Rejected: " prefix, which marks it as a rejection.</summary>
    [Fact]
    public void Reject_WithReason_StoresPrefixedReason()
    {
        var booking = NewBooking();

        booking.Reject("  No photographer available that week.  ", AdminId, ActionUtc);

        Assert.Equal("Rejected: No photographer available that week.", booking.CancellationReason);
        Assert.Equal(booking.CancellationReason, booking.StatusChanges[^1].Note);
    }

    /// <summary>A plain cancellation keeps the reason without the prefix, or none at all.</summary>
    [Theory]
    [InlineData("Plans changed.", "Plans changed.")]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    public void Cancel_Requested_StoresReasonWithoutPrefix(string? reason, string? expected)
    {
        var booking = NewBooking();

        booking.Cancel(reason, ClientId, ActionUtc);

        Assert.Equal(expected, booking.CancellationReason);
    }

    /// <summary>Completing before the slot starts fails and the booking stays Confirmed.</summary>
    [Fact]
    public void Complete_BeforeSlotStart_ThrowsAndStaysConfirmed()
    {
        var booking = BookingIn(BookingStatus.Confirmed);
        var eventsBefore = booking.StatusChanges.Count;

        Assert.Throws<InvalidBookingTransitionException>(
            () => booking.Complete(SlotStartUtc, StaffId, SlotStartUtc.AddMinutes(-1)));

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(eventsBefore, booking.StatusChanges.Count);
    }

    /// <summary>Rescheduling a confirmed booking returns it to Requested, moves the slot and keeps the proposed staff.</summary>
    [Fact]
    public void Reschedule_Confirmed_ReturnsToRequestedAndKeepsStaff()
    {
        var booking = BookingIn(BookingStatus.Confirmed);
        var newSlot = Slot(id: 8, day: 6);

        booking.Reschedule(newSlot, ClientId, ActionUtc);

        Assert.Equal(BookingStatus.Requested, booking.Status);
        Assert.Equal(8, booking.TimeSlotId);
        Assert.Same(newSlot, booking.TimeSlot);
        Assert.Equal(StaffId, booking.StaffUserId);
        Assert.Equal("Moved to 2026-10-06 at 09:00.", booking.StatusChanges[^1].Note);
    }

    /// <summary>The proposed staff member can be confirmed again after a reschedule, or replaced.</summary>
    [Fact]
    public void Confirm_AfterReschedule_CanAssignDifferentStaff()
    {
        var booking = BookingIn(BookingStatus.Confirmed);
        booking.Reschedule(Slot(id: 8, day: 6), ClientId, ActionUtc);

        booking.Confirm(OtherStaffId, AdminId, ActionUtc);

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(OtherStaffId, booking.StaffUserId);
    }

    /// <summary>Completed and Cancelled accept nothing: every action is refused.</summary>
    [Theory]
    [InlineData(BookingStatus.Completed)]
    [InlineData(BookingStatus.Cancelled)]
    public void Act_FinalStatus_RefusesEveryAction(BookingStatus finalStatus)
    {
        foreach (var action in Enum.GetValues<BookingAction>().Where(a => a != BookingAction.Create))
        {
            var booking = BookingIn(finalStatus);

            Assert.Throws<InvalidBookingTransitionException>(() => Act(booking, action));
            Assert.Equal(finalStatus, booking.Status);
        }
    }

    /// <summary>A new Requested booking for a 09:00–11:00 slot on 5 October 2026 (SAST).</summary>
    private static Booking NewBooking()
    {
        var service = new Service { Id = 3, Name = "Professional Product Photoshoot", Category = ServiceCategory.Photoshoot, DurationMinutes = 120 };
        return Booking.Create(ClientId, service, Slot(id: 7, day: 5), "EXS-TEST01", CreatedUtc);
    }

    /// <summary>Builds a booking in the given status through the real domain methods, never by setting Status.</summary>
    private static Booking BookingIn(BookingStatus status)
    {
        var booking = NewBooking();
        switch (status)
        {
            case BookingStatus.Requested:
                break;
            case BookingStatus.Confirmed:
                booking.Confirm(StaffId, AdminId, CreatedUtc);
                break;
            case BookingStatus.Completed:
                booking.Confirm(StaffId, AdminId, CreatedUtc);
                booking.Complete(SlotStartUtc, StaffId, SlotStartUtc);
                break;
            case BookingStatus.Cancelled:
                booking.Cancel("Plans changed.", ClientId, CreatedUtc);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Not a contract status.");
        }

        return booking;
    }

    /// <summary>Calls the domain method for an action, with valid arguments, so only the state decides the outcome.</summary>
    private static void Act(Booking booking, BookingAction action)
    {
        switch (action)
        {
            case BookingAction.Confirm:
                booking.Confirm(StaffId, AdminId, ActionUtc);
                break;
            case BookingAction.Reject:
                booking.Reject("Not available.", AdminId, ActionUtc);
                break;
            case BookingAction.Reschedule:
                booking.Reschedule(Slot(id: 8, day: 6), ClientId, ActionUtc);
                break;
            case BookingAction.Cancel:
                booking.Cancel(null, ClientId, ActionUtc);
                break;
            case BookingAction.Complete:
                booking.Complete(SlotStartUtc, StaffId, ActionUtc);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, "Not a transition action.");
        }
    }

    private static TimeSlot Slot(int id, int day) => new()
    {
        Id = id,
        SlotDate = new DateOnly(2026, 10, day),
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(11, 0),
    };
}
