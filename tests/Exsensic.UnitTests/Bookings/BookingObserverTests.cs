using Exsensic.Contracts.Enums;
using Exsensic.Core.Abstractions;
using Exsensic.Core.Bookings;
using Exsensic.Core.Bookings.Observers;
using Exsensic.Core.Entities;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Exsensic.UnitTests.Bookings;

/// <summary>
/// Checks the Observer pattern: the dispatcher hands every event to every observer once, the history
/// observer writes one row per event, and the notification observer tells the right people.
/// The database context is a substitute, so no SQL Server is needed.
/// </summary>
public class BookingObserverTests
{
    private static readonly Guid ClientId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AdminId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StaffId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset NowUtc = new(2026, 10, 3, 8, 0, 0, TimeSpan.Zero);

    private readonly List<Notification> _added = [];
    private readonly IAppDbContext _db;

    /// <summary>Builds a substitute context whose Notifications set records what is added.</summary>
    public BookingObserverTests()
    {
        var notifications = Substitute.For<DbSet<Notification>>();
        notifications.When(set => set.Add(Arg.Any<Notification>()))
            .Do(call => _added.Add(call.Arg<Notification>()));

        _db = Substitute.For<IAppDbContext>();
        _db.Notifications.Returns(notifications);
    }

    /// <summary>Every recorded event reaches every observer, in order, and the events are then cleared.</summary>
    [Fact]
    public async Task DispatchAsync_TwoEventsTwoObservers_CallsEachObserverForEachEventOnce()
    {
        var booking = SavedBooking();
        booking.Confirm(StaffId, AdminId, NowUtc);
        var first = Substitute.For<IBookingObserver>();
        var second = Substitute.For<IBookingObserver>();
        var dispatcher = new BookingEventDispatcher([first, second]);

        await dispatcher.DispatchAsync(booking, CancellationToken.None);

        await first.Received(2).OnStatusChangedAsync(booking, Arg.Any<BookingStatusChanged>(), Arg.Any<CancellationToken>());
        await second.Received(2).OnStatusChangedAsync(booking, Arg.Any<BookingStatusChanged>(), Arg.Any<CancellationToken>());
        Assert.Empty(booking.StatusChanges);
    }

    /// <summary>Dispatching twice doesn't handle the same events again.</summary>
    [Fact]
    public async Task DispatchAsync_CalledTwice_HandlesEventsOnlyOnce()
    {
        var booking = SavedBooking();
        var observer = Substitute.For<IBookingObserver>();
        var dispatcher = new BookingEventDispatcher([observer]);

        await dispatcher.DispatchAsync(booking, CancellationToken.None);
        await dispatcher.DispatchAsync(booking, CancellationToken.None);

        await observer.Received(1).OnStatusChangedAsync(booking, Arg.Any<BookingStatusChanged>(), Arg.Any<CancellationToken>());
    }

    /// <summary>The history observer writes one row per event with the event's details.</summary>
    [Fact]
    public async Task StatusHistoryObserver_EachEvent_AddsMatchingHistoryRow()
    {
        var booking = SavedBooking();
        booking.Reject("Fully booked that week.", AdminId, NowUtc);
        var dispatcher = new BookingEventDispatcher([new StatusHistoryObserver()]);

        await dispatcher.DispatchAsync(booking, CancellationToken.None);

        Assert.Equal(2, booking.StatusHistory.Count);
        var created = booking.StatusHistory.First();
        Assert.Null(created.FromStatus);
        Assert.Equal(BookingStatus.Requested, created.ToStatus);
        Assert.Equal(ClientId, created.ChangedByUserId);

        var rejected = booking.StatusHistory.Last();
        Assert.Equal(BookingStatus.Requested, rejected.FromStatus);
        Assert.Equal(BookingStatus.Cancelled, rejected.ToStatus);
        Assert.Equal(AdminId, rejected.ChangedByUserId);
        Assert.Equal(NowUtc, rejected.ChangedAtUtc);
        Assert.Equal("Rejected: Fully booked that week.", rejected.Note);
    }

    /// <summary>A new booking tells the client it was received.</summary>
    [Fact]
    public async Task NotificationObserver_Create_NotifiesClient()
    {
        var booking = SavedBooking();

        await Dispatch(booking);

        var notification = Assert.Single(_added);
        Assert.Equal(ClientId, notification.UserId);
        Assert.Equal(NotificationType.BookingRequested, notification.Type);
        Assert.Equal(booking.Id, notification.BookingId);
        Assert.False(notification.IsRead);
        Assert.Contains("EXS-TEST01", notification.Message);
        Assert.Contains("Mon 5 Oct 2026 at 09:00", notification.Message);
    }

    /// <summary>Confirming tells the client it is confirmed and the staff member they were assigned.</summary>
    [Fact]
    public async Task NotificationObserver_Confirm_NotifiesClientAndStaff()
    {
        var booking = SavedBooking();
        await Dispatch(booking);
        _added.Clear();

        booking.Confirm(StaffId, AdminId, NowUtc);
        await Dispatch(booking);

        Assert.Equal(2, _added.Count);
        Assert.Contains(_added, n => n.UserId == ClientId && n.Type == NotificationType.BookingConfirmed);
        Assert.Contains(_added, n => n.UserId == StaffId && n.Type == NotificationType.StaffAssigned);
        Assert.DoesNotContain(_added, n => n.UserId == AdminId);
    }

    /// <summary>A rejection tells the client the reason, without the internal "Rejected: " marker.</summary>
    [Fact]
    public async Task NotificationObserver_Reject_NotifiesClientWithReason()
    {
        var booking = SavedBooking();
        await Dispatch(booking);
        _added.Clear();

        booking.Reject("Fully booked that week.", AdminId, NowUtc);
        await Dispatch(booking);

        var notification = Assert.Single(_added);
        Assert.Equal(NotificationType.BookingRejected, notification.Type);
        Assert.EndsWith("Reason: Fully booked that week.", notification.Message);
        Assert.DoesNotContain("Rejected:", notification.Message);
    }

    /// <summary>A client cancelling their own confirmed booking tells the staff member, but not the client themselves.</summary>
    [Fact]
    public async Task NotificationObserver_ClientCancelsConfirmed_NotifiesStaffOnly()
    {
        var booking = SavedBooking();
        booking.Confirm(StaffId, AdminId, NowUtc);
        await Dispatch(booking);
        _added.Clear();

        booking.Cancel("Plans changed.", ClientId, NowUtc);
        await Dispatch(booking);

        var notification = Assert.Single(_added);
        Assert.Equal(StaffId, notification.UserId);
        Assert.Equal(NotificationType.BookingCancelled, notification.Type);
    }

    /// <summary>Completing tells the client; the staff member who completed it isn't told about their own action.</summary>
    [Fact]
    public async Task NotificationObserver_StaffCompletes_NotifiesClientOnly()
    {
        var booking = SavedBooking();
        booking.Confirm(StaffId, AdminId, NowUtc);
        await Dispatch(booking);
        _added.Clear();

        booking.Complete(NowUtc, StaffId, NowUtc.AddHours(3));
        await Dispatch(booking);

        var notification = Assert.Single(_added);
        Assert.Equal(ClientId, notification.UserId);
        Assert.Equal(NotificationType.BookingCompleted, notification.Type);
    }

    /// <summary>Rescheduling a confirmed booking tells the client and the proposed staff member that it needs approval again.</summary>
    [Fact]
    public async Task NotificationObserver_AdminReschedulesConfirmed_NotifiesClientAndStaff()
    {
        var booking = SavedBooking();
        booking.Confirm(StaffId, AdminId, NowUtc);
        await Dispatch(booking);
        _added.Clear();

        booking.Reschedule(Slot(8, 6), AdminId, NowUtc);
        await Dispatch(booking);

        Assert.Equal(2, _added.Count);
        Assert.All(_added, n => Assert.Equal(NotificationType.BookingRescheduled, n.Type));
        Assert.Contains(_added, n => n.UserId == ClientId && n.Message.Contains("Tue 6 Oct 2026 at 09:00"));
        Assert.Contains(_added, n => n.UserId == StaffId);
    }

    /// <summary>A booking without a database id can't be linked to, so dispatching it is a programming error.</summary>
    [Fact]
    public async Task NotificationObserver_UnsavedBooking_Throws()
    {
        var booking = Booking.Create(ClientId, Service(), Slot(7, 5), "EXS-TEST01", NowUtc);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Dispatch(booking));
    }

    private Task Dispatch(Booking booking) =>
        new BookingEventDispatcher([new StatusHistoryObserver(), new NotificationObserver(_db)])
            .DispatchAsync(booking, CancellationToken.None);

    /// <summary>A new booking that has been given an id, as it would be after its first save.</summary>
    private static Booking SavedBooking()
    {
        var booking = Booking.Create(ClientId, Service(), Slot(7, 5), "EXS-TEST01", NowUtc);
        booking.Id = 42;
        return booking;
    }

    private static Service Service() => new()
    {
        Id = 3,
        Name = "Professional Product Photoshoot",
        Category = ServiceCategory.Photoshoot,
        DurationMinutes = 120,
    };

    private static TimeSlot Slot(int id, int day) => new()
    {
        Id = id,
        SlotDate = new DateOnly(2026, 10, day),
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(11, 0),
    };
}
