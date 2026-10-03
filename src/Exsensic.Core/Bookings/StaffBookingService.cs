using Exsensic.Contracts.Staff;
using Exsensic.Core.Abstractions;
using Exsensic.Core.Bookings.Observers;
using Exsensic.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Exsensic.Core.Bookings;

/// <summary>
/// The staff side of a booking: marking it as delivered (PUT /api/v1/staff/bookings/{id}/complete).
/// Uses the same transaction, RowVersion and observer flow as every other booking change.
/// </summary>
public sealed class StaffBookingService
{
    private readonly IAppDbContext _db;
    private readonly BookingEventDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StaffBookingService> _logger;

    /// <summary>
    /// Creates the service with the database, the observer dispatcher, the clock and a logger.
    /// </summary>
    public StaffBookingService(
        IAppDbContext db,
        BookingEventDispatcher dispatcher,
        TimeProvider timeProvider,
        ILogger<StaffBookingService> logger)
    {
        _db = db;
        _dispatcher = dispatcher;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Marks a confirmed booking as delivered: Confirmed → Completed. The State pattern refuses it before
    /// the slot has started (a service can't have been delivered yet) and for any other status.
    /// </summary>
    /// <param name="bookingId">The booking to complete. The caller's access has already been checked.</param>
    /// <param name="request">The RowVersion the staff member last saw.</param>
    /// <param name="userId">The assigned staff member or admin completing the booking.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    /// <exception cref="InvalidBookingTransitionException">The booking isn't Confirmed, or its slot hasn't started.</exception>
    /// <exception cref="ConcurrencyConflictException">The booking changed since it was loaded.</exception>
    public async Task CompleteAsync(int bookingId, CompleteBookingRequest request, Guid userId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var expectedVersion = BookingRules.DecodeRowVersion(request.RowVersion);
        var nowUtc = _timeProvider.GetUtcNow();

        await BookingRules.ChangeAsync(_db, _dispatcher, bookingId, expectedVersion, (booking, _) =>
        {
            var startsAtUtc = SastTime.ToUtc(booking.TimeSlot.SlotDate, booking.TimeSlot.StartTime);
            booking.Complete(startsAtUtc, userId, nowUtc);
            return Task.CompletedTask;
        }, ct);

        _logger.LogInformation("Booking {BookingId} completed", bookingId);
    }
}
