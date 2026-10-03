using Exsensic.Contracts.Admin;
using Exsensic.Core.Abstractions;
using Exsensic.Core.Bookings.Observers;
using Exsensic.Core.Entities;
using Exsensic.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Exsensic.Core.Bookings;

/// <summary>
/// The admin's booking decisions: approve with a staff member, or reject with a reason
/// (PUT /api/v1/admin/bookings/{id}/confirm and /reject). Uses the same transaction, RowVersion and
/// observer flow as the client actions in <see cref="BookingService"/>.
/// </summary>
public sealed class AdminBookingService
{
    private readonly IAppDbContext _db;
    private readonly BookingEventDispatcher _dispatcher;
    private readonly IUserDirectory _users;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AdminBookingService> _logger;

    /// <summary>
    /// Creates the service with the database, the observer dispatcher, the user directory, the clock and a logger.
    /// </summary>
    public AdminBookingService(
        IAppDbContext db,
        BookingEventDispatcher dispatcher,
        IUserDirectory users,
        TimeProvider timeProvider,
        ILogger<AdminBookingService> logger)
    {
        _db = db;
        _dispatcher = dispatcher;
        _users = users;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Approves a booking and assigns a staff member: Requested → Confirmed. The staff member is checked
    /// again inside the transaction, because the admin's list of options may be out of date by the time
    /// they press Approve.
    /// </summary>
    /// <param name="bookingId">The booking to approve.</param>
    /// <param name="request">The staff member to assign and the RowVersion the admin last saw.</param>
    /// <param name="adminUserId">The admin approving the booking.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    /// <exception cref="InvalidBookingTransitionException">The booking isn't Requested.</exception>
    /// <exception cref="StaffUnavailableException">The staff member isn't qualified, active or free at that time.</exception>
    /// <exception cref="ConcurrencyConflictException">The booking changed since the admin loaded it.</exception>
    public async Task ConfirmAsync(int bookingId, ConfirmBookingRequest request, Guid adminUserId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var expectedVersion = BookingRules.DecodeRowVersion(request.RowVersion);
        var nowUtc = _timeProvider.GetUtcNow();

        await BookingRules.ChangeAsync(_db, _dispatcher, bookingId, expectedVersion, async (booking, token) =>
        {
            // The state is asked first, so confirming a final booking reports invalid_transition.
            booking.Confirm(request.StaffUserId, adminUserId, nowUtc);
            await EnsureStaffCanTakeAsync(request.StaffUserId, booking, token);
        }, ct);

        _logger.LogInformation("Booking {BookingId} confirmed", bookingId);
    }

    /// <summary>
    /// Rejects a booking with a reason the client will see: Requested → Cancelled, stored as "Rejected: …".
    /// </summary>
    /// <param name="bookingId">The booking to reject.</param>
    /// <param name="request">The reason and the RowVersion the admin last saw.</param>
    /// <param name="adminUserId">The admin rejecting the booking.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    /// <exception cref="InvalidBookingTransitionException">The booking isn't Requested.</exception>
    /// <exception cref="ConcurrencyConflictException">The booking changed since the admin loaded it.</exception>
    public async Task RejectAsync(int bookingId, RejectBookingRequest request, Guid adminUserId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var expectedVersion = BookingRules.DecodeRowVersion(request.RowVersion);
        var nowUtc = _timeProvider.GetUtcNow();

        await BookingRules.ChangeAsync(_db, _dispatcher, bookingId, expectedVersion, (booking, _) =>
        {
            booking.Reject(request.Reason, adminUserId, nowUtc);
            return Task.CompletedTask;
        }, ct);

        _logger.LogInformation("Booking {BookingId} rejected", bookingId);
    }

    /// <summary>
    /// Throws <see cref="StaffUnavailableException"/> unless the staff member is qualified for the booking's
    /// service, has an active account, and has no other active booking overlapping its slot.
    /// </summary>
    private async Task EnsureStaffCanTakeAsync(Guid staffUserId, Booking booking, CancellationToken ct)
    {
        var qualified = await StaffAvailability.QualifiedStaffAsync(_db, booking.ServiceId, ct);
        if (!qualified.Contains(staffUserId))
        {
            throw new StaffUnavailableException("That staff member doesn't deliver this service. Please choose someone else.");
        }

        var contacts = await _users.GetContactsAsync([staffUserId], ct);
        if (!contacts.TryGetValue(staffUserId, out var staff) || !staff.IsActive)
        {
            throw new StaffUnavailableException("That staff member's account isn't active. Please choose someone else.");
        }

        var busy = await StaffAvailability.BusyStaffAsync(_db, booking.TimeSlot, booking.Id, ct);
        if (busy.Contains(staffUserId))
        {
            throw new StaffUnavailableException("That staff member already has a booking at this time. Please choose someone else.");
        }
    }
}
