using Exsensic.Contracts.Enums;
using Exsensic.Core.Abstractions;
using Exsensic.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Exsensic.Core.Bookings;

/// <summary>
/// Which staff members can deliver a booking: qualified for the service (a StaffServices link,
/// docs/CONTRACTS.md §12 decision 7) and free at that time (no other Requested or Confirmed booking whose
/// slot overlaps). Shared by the staff options list and by Confirm, so both apply exactly the same rule.
/// </summary>
internal static class StaffAvailability
{
    /// <summary>
    /// The staff members linked to the service who have a staff profile.
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> QualifiedStaffAsync(IAppDbContext db, int serviceId, CancellationToken ct) =>
        await db.StaffServices.AsNoTracking()
            .Where(link => link.ServiceId == serviceId && db.StaffProfiles.Any(p => p.UserId == link.StaffUserId))
            .Select(link => link.StaffUserId)
            .ToListAsync(ct);

    /// <summary>
    /// The staff members who already have a Requested or Confirmed booking overlapping the slot's time.
    /// Two slots overlap when each starts before the other ends.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="slot">The slot being staffed.</param>
    /// <param name="exceptBookingId">The booking being confirmed, which mustn't count against its own staff member.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    public static async Task<IReadOnlySet<Guid>> BusyStaffAsync(IAppDbContext db, TimeSlot slot, int? exceptBookingId, CancellationToken ct)
    {
        var date = slot.SlotDate;
        var start = slot.StartTime;
        var end = slot.EndTime;

        var busy = await db.Bookings.AsNoTracking()
            .Where(b => b.StaffUserId != null
                && b.Id != exceptBookingId
                && (b.Status == BookingStatus.Requested || b.Status == BookingStatus.Confirmed)
                && b.TimeSlot.SlotDate == date
                && b.TimeSlot.StartTime < end
                && start < b.TimeSlot.EndTime)
            .Select(b => b.StaffUserId!.Value)
            .Distinct()
            .ToListAsync(ct);

        return busy.ToHashSet();
    }
}
