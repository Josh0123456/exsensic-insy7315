using Exsensic.Contracts.Catalog;
using Exsensic.Contracts.Common;
using Exsensic.Contracts.Enums;
using Exsensic.Core.Abstractions;
using Exsensic.Core.Entities;
using Exsensic.Core.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Exsensic.Core.Catalog;

/// <summary>
/// The public service catalogue and slot availability (docs/CONTRACTS.md §4 and §6).
/// Availability is always worked out from the data, never stored, so it can never drift out of date.
/// </summary>
public sealed class ServiceCatalogService(IAppDbContext db, TimeProvider timeProvider)
{
    /// <summary>The longest availability range a caller may ask for at once.</summary>
    public const int MaxAvailabilityDays = 60;

    /// <summary>Slot dates and times are South African local time (SAST, UTC+2), docs/CONTRACTS.md §4.</summary>
    private static readonly TimeSpan Sast = TimeSpan.FromHours(2);

    /// <summary>Active services, ordered by category and then name.</summary>
    public async Task<IReadOnlyList<ServiceDto>> ListActiveAsync(CancellationToken ct)
    {
        var services = await db.Services.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Category).ThenBy(s => s.Name)
            .Select(s => new { s.Id, s.Name, s.Category, s.Description, s.DurationMinutes, s.BasePrice, s.IsActive, s.RowVersion })
            .ToListAsync(ct);

        return services.Select(s => ToDto(s.Id, s.Name, s.Category, s.Description, s.DurationMinutes, s.BasePrice, s.IsActive, s.RowVersion)).ToList();
    }

    /// <summary>One active service; a missing or archived service is 404 not_found.</summary>
    public async Task<ServiceDto> GetActiveAsync(int id, CancellationToken ct)
    {
        var s = await db.Services.AsNoTracking()
            .Where(s => s.Id == id && s.IsActive)
            .Select(s => new { s.Id, s.Name, s.Category, s.Description, s.DurationMinutes, s.BasePrice, s.IsActive, s.RowVersion })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Service");

        return ToDto(s.Id, s.Name, s.Category, s.Description, s.DurationMinutes, s.BasePrice, s.IsActive, s.RowVersion);
    }

    /// <summary>
    /// Slots in [from, to] that can be booked for the service: not blocked, starting after now (SAST),
    /// with no Requested or Confirmed booking, and at least as long as the service's duration.
    /// The caller must already have checked the range is valid (see <see cref="MaxAvailabilityDays"/>).
    /// </summary>
    public async Task<IReadOnlyList<AvailableSlotDto>> AvailabilityAsync(int serviceId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var duration = await db.Services.AsNoTracking()
            .Where(s => s.Id == serviceId && s.IsActive)
            .Select(s => (int?)s.DurationMinutes)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Service");

        var nowSast = timeProvider.GetUtcNow().ToOffset(Sast);
        var today = DateOnly.FromDateTime(nowSast.DateTime);
        var timeNow = TimeOnly.FromDateTime(nowSast.DateTime);

        var slots = await db.TimeSlots.AsNoTracking()
            .Where(t => t.SlotDate >= from && t.SlotDate <= to && !t.IsBlocked)
            .Where(t => t.SlotDate > today || (t.SlotDate == today && t.StartTime > timeNow))
            .Where(t => !db.Bookings.Any(b => b.TimeSlotId == t.Id
                && (b.Status == BookingStatus.Requested || b.Status == BookingStatus.Confirmed)))
            .OrderBy(t => t.SlotDate).ThenBy(t => t.StartTime)
            .Select(t => new AvailableSlotDto(t.Id, t.SlotDate, t.StartTime, t.EndTime))
            .ToListAsync(ct);

        // Long enough for the service. Done in memory because TimeOnly subtraction is not translated to SQL.
        return slots.Where(s => (s.EndTime - s.StartTime).TotalMinutes >= duration).ToList();
    }

    /// <summary>
    /// Every service, active or not, for the admin list.
    /// </summary>
    public async Task<IReadOnlyList<ServiceDto>> ListAllAsync(CancellationToken ct)
    {
        var services = await db.Services.AsNoTracking()
            .OrderBy(s => s.Category).ThenBy(s => s.Name)
            .Select(s => new { s.Id, s.Name, s.Category, s.Description, s.DurationMinutes, s.BasePrice, s.IsActive, s.RowVersion })
            .ToListAsync(ct);

        return services.Select(s => ToDto(s.Id, s.Name, s.Category, s.Description, s.DurationMinutes, s.BasePrice, s.IsActive, s.RowVersion)).ToList();
    }

    /// <summary>
    /// Creates a new service and returns its id. Name must be unique.
    /// </summary>
    public async Task<int> CreateAsync(SaveServiceRequest request, CancellationToken ct)
    {
        if (await db.Services.AnyAsync(s => s.Name == request.Name, ct))
        {
            throw new BusinessRuleException(ErrorCodes.Duplicate, "A service with this name already exists.");
        }

        var service = new Core.Entities.Service
        {
            Name = request.Name.Trim(),
            Category = Enum.Parse<ServiceCategory>(request.Category),
            Description = request.Description.Trim(),
            DurationMinutes = request.DurationMinutes,
            BasePrice = request.BasePrice,
            IsActive = true
        };

        db.Services.Add(service);
        await db.SaveChangesAsync(ct);
        return service.Id;
    }

    /// <summary>
    /// Updates an existing service. Archived services can still be edited.
    /// </summary>
    public async Task UpdateAsync(int id, SaveServiceRequest request, CancellationToken ct)
    {
        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Service");

        // Changing the name to one another service already has is a conflict.
        if (await db.Services.AnyAsync(s => s.Id != id && s.Name == request.Name, ct))
        {
            throw new BusinessRuleException(ErrorCodes.Duplicate, "A service with this name already exists.");
        }

        service.Name = request.Name.Trim();
        service.Category = Enum.Parse<ServiceCategory>(request.Category);
        service.Description = request.Description.Trim();
        service.DurationMinutes = request.DurationMinutes;
        service.BasePrice = request.BasePrice;

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Turns a service on or off. Off hides it from the public catalogue but keeps history.
    /// </summary>
    public async Task SetActiveAsync(int id, bool isActive, string rowVersion, CancellationToken ct)
    {
        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Service");

        var expected = Convert.FromBase64String(rowVersion);
        if (!service.RowVersion.AsSpan().SequenceEqual(expected))
        {
            throw new ConcurrencyConflictException();
        }

        service.IsActive = isActive;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Every slot in a range, with whether it is blocked and whether it has an active booking.
    /// </summary>
    public async Task<IReadOnlyList<TimeSlotDto>> ListTimeSlotsAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var query = db.TimeSlots.AsNoTracking();
        if (from is DateOnly f) query = query.Where(t => t.SlotDate >= f);
        if (to is DateOnly t2) query = query.Where(t => t.SlotDate <= t2);

        var slots = await query
            .OrderBy(t => t.SlotDate).ThenBy(t => t.StartTime)
            .Select(t => new
            {
                t.Id,
                t.SlotDate,
                t.StartTime,
                t.EndTime,
                t.IsBlocked,
                t.BlockReason,
                t.RowVersion,
                HasActiveBooking = db.Bookings.Any(b => b.TimeSlotId == t.Id
                    && (b.Status == BookingStatus.Requested || b.Status == BookingStatus.Confirmed))
            })
            .ToListAsync(ct);

        return slots.Select(t => new TimeSlotDto(
            t.Id, t.SlotDate, t.StartTime, t.EndTime,
            t.IsBlocked, t.BlockReason, t.HasActiveBooking,
            Convert.ToBase64String(t.RowVersion))).ToList();
    }

    /// <summary>
    /// Creates slots on the chosen weekdays between two dates. Skips any (date, start) that already exists.
    /// </summary>
    public async Task<GenerateSlotsResult> GenerateSlotsAsync(GenerateSlotsRequest request, CancellationToken ct)
    {
        if (request.ToDate < request.FromDate)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["ToDate"] = ["End date must be on or after the start date."]
            });
        }

        if (request.ToDate.DayNumber - request.FromDate.DayNumber > 90)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["ToDate"] = ["Ask for at most 90 days at a time."]
            });
        }

        var created = 0;
        var skipped = 0;

        for (var date = request.FromDate; date <= request.ToDate; date = date.AddDays(1))
        {
            if (!request.Weekdays.Contains(date.DayOfWeek)) continue;

            foreach (var start in request.StartTimes)
            {
                var exists = await db.TimeSlots.AnyAsync(t => t.SlotDate == date && t.StartTime == start, ct);
                if (exists)
                {
                    skipped++;
                    continue;
                }

                db.TimeSlots.Add(new Core.Entities.TimeSlot
                {
                    SlotDate = date,
                    StartTime = start,
                    EndTime = start.AddMinutes(request.DurationMinutes)
                });
                created++;
            }
        }

        await db.SaveChangesAsync(ct);
        return new GenerateSlotsResult(created, skipped);
    }

    /// <summary>
    /// Blocks a slot with a reason.
    /// </summary>
    public async Task BlockSlotAsync(int id, string reason, string rowVersion, CancellationToken ct)
    {
        var slot = await db.TimeSlots.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Time slot");

        var expected = Convert.FromBase64String(rowVersion);
        if (!slot.RowVersion.AsSpan().SequenceEqual(expected))
        {
            throw new ConcurrencyConflictException();
        }

        slot.IsBlocked = true;
        slot.BlockReason = reason.Trim();
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Removes a block from a slot.
    /// </summary>
    public async Task UnblockSlotAsync(int id, CancellationToken ct)
    {
        var slot = await db.TimeSlots.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Time slot");

        slot.IsBlocked = false;
        slot.BlockReason = null;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Deletes a slot, only if it has no active booking.
    /// </summary>
    public async Task DeleteSlotAsync(int id, CancellationToken ct)
    {
        var slot = await db.TimeSlots.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Time slot");

        var hasActive = await db.Bookings.AnyAsync(b => b.TimeSlotId == id
            && (b.Status == BookingStatus.Requested || b.Status == BookingStatus.Confirmed), ct);

        if (hasActive)
        {
            throw new BusinessRuleException(ErrorCodes.SlotHasBooking, "This slot has an active booking and can't be deleted.");
        }

        db.TimeSlots.Remove(slot);
        await db.SaveChangesAsync(ct);
    }

    private static ServiceDto ToDto(int id, string name, ServiceCategory category, string description,
        int durationMinutes, decimal? basePrice, bool isActive, byte[] rowVersion) =>
        new(id, name, category.ToString(), description, durationMinutes, basePrice, isActive, Convert.ToBase64String(rowVersion));
}
