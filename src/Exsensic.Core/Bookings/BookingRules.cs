using Exsensic.Contracts.Enums;
using Exsensic.Core.Abstractions;
using Exsensic.Core.Bookings.Observers;
using Exsensic.Core.Entities;
using Exsensic.Core.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Exsensic.Core.Bookings;

/// <summary>
/// Checks shared by every booking change (create, reschedule, cancel, confirm, reject, complete), kept in
/// one place so the client and admin services apply exactly the same rules.
/// </summary>
internal static class BookingRules
{
    /// <summary>
    /// The filtered unique index that allows only one Requested or Confirmed booking per slot
    /// (docs/CONTRACTS.md §4). It is the real double-booking guard: the checks in code give a friendly
    /// message, but only the database can stop two requests that arrive at the same moment.
    /// </summary>
    public const string ActiveSlotIndex = "UX_Bookings_ActiveSlot";

    /// <summary>
    /// The shared shape of every change to an existing booking: inside one transaction, load the booking,
    /// check the client's RowVersion, apply the change, let the observers add history and notifications,
    /// and save everything together. Either all of it is stored or none of it is.
    /// </summary>
    public static Task ChangeAsync(
        IAppDbContext db,
        BookingEventDispatcher dispatcher,
        int bookingId,
        byte[] expectedVersion,
        Func<Booking, CancellationToken, Task> change,
        CancellationToken ct) =>
        db.InTransactionAsync(async token =>
        {
            var booking = await LoadForChangeAsync(db, bookingId, token);
            EnsureRowVersionMatches(booking, expectedVersion);

            await change(booking, token);

            await dispatcher.DispatchAsync(booking, token);
            await db.SaveChangesAsync(token);
            return booking.Id;
        }, ct);

    /// <summary>
    /// Loads a booking for changing, with tracking and with its service and slot, or throws not found.
    /// </summary>
    public static async Task<Booking> LoadForChangeAsync(IAppDbContext db, int bookingId, CancellationToken ct) =>
        await db.Bookings
            .Include(b => b.Service)
            .Include(b => b.TimeSlot)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct)
        ?? throw new NotFoundException("Booking");

    /// <summary>
    /// Turns the RowVersion the client sent back into bytes. A value that isn't valid base64 is a
    /// 400 validation error on the RowVersion field, not a server error.
    /// </summary>
    public static byte[] DecodeRowVersion(string rowVersion)
    {
        try
        {
            return Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["RowVersion"] = ["The booking version is not valid. Please reload the booking and try again."],
            });
        }
    }

    /// <summary>
    /// Optimistic concurrency: the client must send the RowVersion it last saw. If the booking has changed
    /// since (another tab, the admin, the client), the change is refused with 409 concurrency_conflict
    /// instead of silently overwriting someone else's work. A change that sneaks in between this check
    /// and the save is caught by EF Core's own RowVersion check (DbUpdateConcurrencyException, also 409).
    /// </summary>
    public static void EnsureRowVersionMatches(Booking booking, byte[] expected)
    {
        if (!booking.RowVersion.AsSpan().SequenceEqual(expected))
        {
            throw new ConcurrencyConflictException("This booking was changed by someone else. Please reload it and try again.");
        }
    }

    /// <summary>
    /// The slot rules from docs/CONTRACTS.md §4: not blocked, starting in the future, and at least as long
    /// as the service. The "no active booking" rule is checked separately against the database.
    /// </summary>
    public static void EnsureSlotCanBeBooked(TimeSlot slot, Service service, DateTimeOffset nowUtc)
    {
        if (slot.IsBlocked)
        {
            throw new SlotUnavailableException("That time slot isn't available. Please choose another time.");
        }

        if (SastTime.ToUtc(slot.SlotDate, slot.StartTime) <= nowUtc)
        {
            throw new SlotUnavailableException("That time slot has already started. Please choose a later time.");
        }

        if ((slot.EndTime - slot.StartTime).TotalMinutes < service.DurationMinutes)
        {
            throw new SlotUnavailableException("That time slot is too short for this service. Please choose another time.");
        }
    }

    /// <summary>
    /// Throws if another Requested or Confirmed booking already holds the slot.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="timeSlotId">The slot to check.</param>
    /// <param name="exceptBookingId">A booking to ignore (the one being moved), or null.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    public static async Task EnsureSlotIsFreeAsync(IAppDbContext db, int timeSlotId, int? exceptBookingId, CancellationToken ct)
    {
        var taken = await db.Bookings.AnyAsync(
            b => b.TimeSlotId == timeSlotId
                && b.Id != exceptBookingId
                && (b.Status == BookingStatus.Requested || b.Status == BookingStatus.Confirmed),
            ct);

        if (taken)
        {
            throw new SlotUnavailableException("Someone has just booked that time. Please choose another time.");
        }
    }

    /// <summary>
    /// True when the save failed because it would break the named unique index. SQL Server reports this as
    /// error 2601 or 2627 with the index name in the message; the name is checked so a clash on any other
    /// index (for example a duplicate reference) is not mistaken for a taken slot.
    /// </summary>
    public static bool IsUniqueIndexViolation(DbUpdateException ex, string indexName)
    {
        for (Exception? inner = ex.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner.Message.Contains(indexName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
