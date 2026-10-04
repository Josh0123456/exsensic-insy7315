using System.Globalization;
using Exsensic.Contracts.Enums;
using Exsensic.Core.Abstractions;
using Exsensic.Core.Entities;

namespace Exsensic.Core.Bookings.Observers;

/// <summary>
/// Creates in-app notifications when a booking changes (docs/CONTRACTS.md §12, decision 11: in-app, not
/// email). The client hears about every change to their booking; the assigned staff member hears when
/// they are assigned, and when a booking they were assigned to is moved or cancelled. Admins see new
/// requests in the dashboard's approval queue instead of a notification.
/// </summary>
public sealed class NotificationObserver : IBookingObserver
{
    private readonly IAppDbContext _db;

    /// <summary>
    /// Creates the observer with the database context the notifications are added to.
    /// </summary>
    /// <param name="db">The application's database context.</param>
    public NotificationObserver(IAppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Adds the notifications for one event. Messages are plain text with the booking reference, service
    /// and time only: no personal data and no requirement text.
    /// </summary>
    /// <param name="booking">The booking that changed. It must already have its database id.</param>
    /// <param name="change">The event to notify about.</param>
    /// <param name="ct">Not used; rows are only added to the context here.</param>
    /// <exception cref="InvalidOperationException">The booking has not been saved yet, so it has no id to link to.</exception>
    public Task OnStatusChangedAsync(Booking booking, BookingStatusChanged change, CancellationToken ct)
    {
        if (booking.Id == 0)
        {
            throw new InvalidOperationException("Save a new booking before dispatching its events, so notifications can link to it.");
        }

        var what = Describe(booking);

        switch (change.Action)
        {
            case BookingAction.Create:
                Notify(booking, booking.ClientUserId, NotificationType.BookingRequested,
                    $"We received your booking {what}. It is waiting for approval.", change);
                break;

            case BookingAction.Confirm:
                Notify(booking, booking.ClientUserId, NotificationType.BookingConfirmed,
                    $"Your booking {what} is confirmed.", change);
                NotifyStaff(booking, NotificationType.StaffAssigned,
                    $"You have been assigned to booking {what}.", change);
                break;

            case BookingAction.Reject:
                Notify(booking, booking.ClientUserId, NotificationType.BookingRejected,
                    $"Your booking {what} could not be accepted. Reason: {RejectionReason(booking)}", change);
                break;

            case BookingAction.Reschedule:
                Notify(booking, booking.ClientUserId, NotificationType.BookingRescheduled,
                    $"Your booking has moved to {what}. It is waiting for approval.", change);
                NotifyStaff(booking, NotificationType.BookingRescheduled,
                    $"Booking {booking.Reference} has moved to {Describe(booking.TimeSlot)} and needs approval again.", change);
                break;

            case BookingAction.Cancel:
                Notify(booking, booking.ClientUserId, NotificationType.BookingCancelled,
                    $"Your booking {what} has been cancelled.", change);
                NotifyStaff(booking, NotificationType.BookingCancelled,
                    $"Booking {what} has been cancelled.", change);
                break;

            case BookingAction.Complete:
                Notify(booking, booking.ClientUserId, NotificationType.BookingCompleted,
                    $"Your booking {what} is complete. Thank you for choosing Exsensic.", change);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(change), change.Action, "Unknown booking action.");
        }

        return Task.CompletedTask;
    }

    /// <summary>Notifies the assigned staff member, if there is one and they didn't make the change themselves.</summary>
    private void NotifyStaff(Booking booking, NotificationType type, string message, BookingStatusChanged change)
    {
        if (booking.StaffUserId is Guid staffUserId)
        {
            Notify(booking, staffUserId, type, message, change);
        }
    }

    /// <summary>Adds one unread notification, unless the recipient is the person who made the change.</summary>
    private void Notify(Booking booking, Guid userId, NotificationType type, string message, BookingStatusChanged change)
    {
        // Nobody needs to be told about their own action, except a client confirming their new booking.
        if (userId == change.ChangedByUserId && change.Action != BookingAction.Create)
        {
            return;
        }

        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            BookingId = booking.Id,
            Type = type,
            Message = message.Trim(),
            IsRead = false,
            CreatedAtUtc = change.ChangedAtUtc,
        });
    }

    /// <summary>The admin's reason without the "Rejected: " marker, which is for the system, not the client.</summary>
    private static string RejectionReason(Booking booking)
    {
        var reason = booking.CancellationReason ?? string.Empty;
        return reason.StartsWith(Booking.RejectionPrefix, StringComparison.Ordinal)
            ? reason[Booking.RejectionPrefix.Length..]
            : reason;
    }

    /// <summary>"EXS-123 (Website Design Consultation, Mon 5 Oct 2026 at 09:00)", or just the reference if not loaded.</summary>
    private static string Describe(Booking booking)
    {
        // The navigations may not be loaded, so they are checked even though they are declared non-null.
        var service = booking.Service?.Name;
        return service is null || booking.TimeSlot is null
            ? booking.Reference
            : $"{booking.Reference} ({service}, {Describe(booking.TimeSlot)})";
    }

    /// <summary>"Mon 5 Oct 2026 at 09:00", in SAST as stored. Invariant culture so the server's settings don't matter.</summary>
    private static string Describe(TimeSlot? slot) =>
        slot is null
            ? "a new time"
            : string.Create(CultureInfo.InvariantCulture, $"{slot.SlotDate:ddd d MMM yyyy} at {slot.StartTime:HH\\:mm}");
}
