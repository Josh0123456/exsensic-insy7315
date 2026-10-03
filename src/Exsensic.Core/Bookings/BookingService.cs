using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Enums;
using Exsensic.Core.Abstractions;
using Exsensic.Core.Bookings.Observers;
using Exsensic.Core.Entities;
using Exsensic.Core.Exceptions;
using Exsensic.Core.Requirements;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Exsensic.Core.Bookings;

/// <summary>
/// The booking use cases the API calls. Controllers stay thin: they pass the request and the signed-in
/// user's id, and this class applies the business rules (docs/CONTRACTS.md §10).
/// </summary>
public sealed class BookingService
{
    /// <summary>
    /// The filtered unique index that allows only one Requested or Confirmed booking per slot
    /// (docs/CONTRACTS.md §4). It is the real double-booking guard: the checks in code give a friendly
    /// message, but only the database can stop two requests that arrive at the same moment.
    /// </summary>
    public const string ActiveSlotIndex = "UX_Bookings_ActiveSlot";

    private readonly IAppDbContext _db;
    private readonly BookingEventDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BookingService> _logger;

    /// <summary>
    /// Creates the service with the database, the observer dispatcher, the clock and a logger.
    /// </summary>
    public BookingService(IAppDbContext db, BookingEventDispatcher dispatcher, TimeProvider timeProvider, ILogger<BookingService> logger)
    {
        _db = db;
        _dispatcher = dispatcher;
        _timeProvider = timeProvider;
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

                EnsureSlotCanBeBooked(slot, service, nowUtc);

                var taken = await _db.Bookings.AnyAsync(
                    b => b.TimeSlotId == slot.Id && (b.Status == BookingStatus.Requested || b.Status == BookingStatus.Confirmed),
                    token);
                if (taken)
                {
                    throw new SlotUnavailableException("Someone has just booked that time. Please choose another time.");
                }

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
        catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex, ActiveSlotIndex))
        {
            _logger.LogInformation("Slot {TimeSlotId} was taken by a concurrent booking", request.TimeSlotId);
            throw new SlotUnavailableException("Someone has just booked that time. Please choose another time.");
        }
    }

    /// <summary>
    /// The slot rules from docs/CONTRACTS.md §4: not blocked, starting in the future, and at least as long
    /// as the service. The "no active booking" rule is checked separately against the database.
    /// </summary>
    private static void EnsureSlotCanBeBooked(TimeSlot slot, Service service, DateTimeOffset nowUtc)
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
    /// True when the save failed because it would break the named unique index. SQL Server reports this as
    /// error 2601 or 2627 with the index name in the message; the name is checked so a clash on any other
    /// index (for example a duplicate reference) is not mistaken for a taken slot.
    /// </summary>
    private static bool IsUniqueIndexViolation(DbUpdateException ex, string indexName)
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
