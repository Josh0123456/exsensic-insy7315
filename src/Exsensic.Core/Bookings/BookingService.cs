using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Common;
using Exsensic.Contracts.Enums;
using Exsensic.Core.Abstractions;
using Exsensic.Core.Bookings.Observers;
using Exsensic.Core.Entities;
using Exsensic.Core.Exceptions;
using Exsensic.Core.Requirements;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Exsensic.Core.Bookings;

/// <summary>
/// The client-side booking use cases the API calls: create, reschedule and cancel. Controllers stay thin:
/// they check access, pass the request and the signed-in user's id, and this class applies the business
/// rules (docs/CONTRACTS.md §10). Admin approval lives in AdminBookingService.
/// </summary>
public sealed class BookingService
{
    private readonly IAppDbContext _db;
    private readonly BookingEventDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;
    private readonly BookingPolicyOptions _policy;
    private readonly ILogger<BookingService> _logger;

    /// <summary>
    /// Creates the service with the database, the observer dispatcher, the clock, the booking policy and a logger.
    /// </summary>
    public BookingService(
        IAppDbContext db,
        BookingEventDispatcher dispatcher,
        TimeProvider timeProvider,
        IOptions<BookingPolicyOptions> policy,
        ILogger<BookingService> logger)
    {
        _db = db;
        _dispatcher = dispatcher;
        _timeProvider = timeProvider;
        _policy = policy.Value;
        _logger = logger;
    }

    /// <summary>
    /// Creates a booking in Requested status for the signed-in client (POST /api/v1/bookings).
    /// </summary>
    /// <remarks>
    /// Order of work: check the service exists, validate the requirement answers, then inside one
    /// transaction load and check the slot, create the booking with its requirements, save it so it has
    /// an id, let the observers add the history row and notification, and save again. Either everything
    /// is stored or nothing is. If another client takes the slot at the same moment, the unique index
    /// rejects the second save and the client gets 409 slot_unavailable.
    /// </remarks>
    /// <param name="request">The service, slot and requirement answers from the booking wizard.</param>
    /// <param name="clientUserId">The signed-in client making the booking.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    /// <returns>The new booking's id, reference and status.</returns>
    /// <exception cref="NotFoundException">The service doesn't exist or is archived.</exception>
    /// <exception cref="RequestValidationException">The requirement answers don't match the service's template.</exception>
    /// <exception cref="SlotUnavailableException">The slot is missing, blocked, started, too short or already booked.</exception>
    public async Task<BookingCreatedDto> CreateAsync(CreateBookingRequest request, Guid clientUserId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var category = await _db.Services.AsNoTracking()
            .Where(s => s.Id == request.ServiceId && s.IsActive)
            .Select(s => (ServiceCategory?)s.Category)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Service");

        // Validated before the transaction starts, so bad input never holds database locks.
        var errors = RequirementValidator.Validate(category, request.Requirements);
        if (errors.Count > 0)
        {
            // Keyed the way the Web form names its fields, so each message appears beside its input.
            throw new RequestValidationException(errors.ToDictionary(e => $"Requirements[{e.Key}]", e => e.Value));
        }

        var nowUtc = _timeProvider.GetUtcNow();

        try
        {
            var created = await _db.InTransactionAsync(async token =>
            {
                // Loaded with tracking, because the new booking points at these entities.
                var service = await _db.Services.FirstOrDefaultAsync(s => s.Id == request.ServiceId && s.IsActive, token)
                    ?? throw new NotFoundException("Service");
                var slot = await _db.TimeSlots.FirstOrDefaultAsync(t => t.Id == request.TimeSlotId, token)
                    ?? throw new SlotUnavailableException();

                BookingRules.EnsureSlotCanBeBooked(slot, service, nowUtc);
                await BookingRules.EnsureSlotIsFreeAsync(_db, slot.Id, exceptBookingId: null, token);

                var booking = Booking.Create(clientUserId, service, slot, BookingReferenceGenerator.Next(), nowUtc);
                foreach (var (key, value) in request.Requirements.Where(a => !string.IsNullOrWhiteSpace(a.Value)))
                {
                    booking.Requirements.Add(new BookingRequirement { FieldKey = key, FieldValue = value.Trim() });
                }

                _db.Bookings.Add(booking);
                await _db.SaveChangesAsync(token);

                // The booking now has its id, which the notification links to.
                await _dispatcher.DispatchAsync(booking, token);
                await _db.SaveChangesAsync(token);

                return booking;
            }, ct);

            _logger.LogInformation("Booking {BookingId} created for service {ServiceId} in slot {TimeSlotId}",
                created.Id, request.ServiceId, request.TimeSlotId);

            return new BookingCreatedDto(created.Id, created.Reference, created.Status);
        }
        catch (DbUpdateException ex) when (BookingRules.IsUniqueIndexViolation(ex, BookingRules.ActiveSlotIndex))
        {
            _logger.LogInformation("Slot {TimeSlotId} was taken by a concurrent booking", request.TimeSlotId);
            throw new SlotUnavailableException("Someone has just booked that time. Please choose another time.");
        }
    }

    /// <summary>
    /// Moves a booking to another slot (PUT /api/v1/bookings/{id}/reschedule). The State pattern decides
    /// what happens to the status: Requested stays Requested, Confirmed goes back to Requested for
    /// re-approval, and final bookings can't be moved. The old slot is freed automatically, because only
    /// Requested and Confirmed bookings hold a slot.
    /// </summary>
    /// <param name="bookingId">The booking to move. The caller's access has already been checked.</param>
    /// <param name="request">The new slot and the RowVersion the client last saw.</param>
    /// <param name="userId">The client or admin making the change.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    /// <exception cref="InvalidBookingTransitionException">The booking is Completed or Cancelled.</exception>
    /// <exception cref="SlotUnavailableException">The new slot is missing, the same, blocked, started, too short or taken.</exception>
    /// <exception cref="ConcurrencyConflictException">The booking changed since the client loaded it.</exception>
    public async Task RescheduleAsync(int bookingId, RescheduleBookingRequest request, Guid userId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var expectedVersion = BookingRules.DecodeRowVersion(request.RowVersion);
        var nowUtc = _timeProvider.GetUtcNow();

        try
        {
            await BookingRules.ChangeAsync(_db, _dispatcher, bookingId, expectedVersion, async (booking, token) =>
            {
                var slot = await _db.TimeSlots.FirstOrDefaultAsync(t => t.Id == request.NewTimeSlotId, token)
                    ?? throw new SlotUnavailableException();
                if (slot.Id == booking.TimeSlotId)
                {
                    throw new SlotUnavailableException("The booking is already at that time. Please choose a different time.");
                }

                // The state is asked first, so moving a final booking reports invalid_transition.
                booking.Reschedule(slot, userId, nowUtc);
                BookingRules.EnsureSlotCanBeBooked(slot, booking.Service, nowUtc);
                await BookingRules.EnsureSlotIsFreeAsync(_db, slot.Id, booking.Id, token);
            }, ct);
        }
        catch (DbUpdateException ex) when (BookingRules.IsUniqueIndexViolation(ex, BookingRules.ActiveSlotIndex))
        {
            throw new SlotUnavailableException("Someone has just booked that time. Please choose another time.");
        }

        _logger.LogInformation("Booking {BookingId} rescheduled to slot {TimeSlotId}", bookingId, request.NewTimeSlotId);
    }

    /// <summary>
    /// Cancels a booking (PUT /api/v1/bookings/{id}/cancel). A client can't cancel a confirmed booking
    /// within BookingPolicy:ClientCancelCutoffHours of its start, because staff have already prepared for
    /// it (409 cancel_window_closed); an admin can cancel at any time.
    /// </summary>
    /// <param name="bookingId">The booking to cancel. The caller's access has already been checked.</param>
    /// <param name="request">An optional reason and the RowVersion the client last saw.</param>
    /// <param name="userId">The client or admin cancelling.</param>
    /// <param name="isAdmin">True when the caller is an admin, who isn't bound by the cut-off.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    /// <exception cref="BusinessRuleException">A client is inside the cut-off window (cancel_window_closed).</exception>
    /// <exception cref="InvalidBookingTransitionException">The booking is already Completed or Cancelled.</exception>
    /// <exception cref="ConcurrencyConflictException">The booking changed since the client loaded it.</exception>
    public async Task CancelAsync(int bookingId, CancelBookingRequest request, Guid userId, bool isAdmin, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var expectedVersion = BookingRules.DecodeRowVersion(request.RowVersion);
        var nowUtc = _timeProvider.GetUtcNow();
        var cutoff = TimeSpan.FromHours(_policy.ClientCancelCutoffHours);

        await BookingRules.ChangeAsync(_db, _dispatcher, bookingId, expectedVersion, (booking, _) =>
        {
            var startsAtUtc = SastTime.ToUtc(booking.TimeSlot.SlotDate, booking.TimeSlot.StartTime);
            if (!isAdmin && booking.Status == BookingStatus.Confirmed && startsAtUtc - nowUtc < cutoff)
            {
                throw new BusinessRuleException(
                    ErrorCodes.CancelWindowClosed,
                    $"Bookings can't be cancelled within {_policy.ClientCancelCutoffHours} hours of the start. Please contact us instead.");
            }

            booking.Cancel(request.Reason, userId, nowUtc);
            return Task.CompletedTask;
        }, ct);

        _logger.LogInformation("Booking {BookingId} cancelled", bookingId);
    }
}
