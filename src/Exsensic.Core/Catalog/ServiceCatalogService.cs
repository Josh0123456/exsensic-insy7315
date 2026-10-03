using Exsensic.Contracts.Catalog;
using Exsensic.Contracts.Enums;
using Exsensic.Core.Abstractions;
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

    private static ServiceDto ToDto(int id, string name, ServiceCategory category, string description,
        int durationMinutes, decimal? basePrice, bool isActive, byte[] rowVersion) =>
        new(id, name, category.ToString(), description, durationMinutes, basePrice, isActive, Convert.ToBase64String(rowVersion));
}
