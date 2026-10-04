using Exsensic.Contracts.Enums;
using Exsensic.Core.Bookings;
using Exsensic.Core.Bookings.Observers;
using Exsensic.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Exsensic.Data.Seed;

/// <summary>
/// Adds the sample bookings the demo and the end-to-end tests rely on (Team Plan, P3 step 16):
/// two Requested, two Confirmed, one Completed, one rejected (Cancelled with "Rejected: …"), and, every
/// day, one Confirmed photographer booking whose slot started earlier today so staff can complete it live.
/// Bookings are created and moved through the real State pattern, and their events go through the
/// observers, so the status history and notifications are real. Idempotent: each booking has a fixed
/// reference and is only added if that reference does not exist yet.
/// </summary>
public static class DemoBookingSeeder
{
    private static readonly TimeSpan Sast = TimeSpan.FromHours(2);

    /// <summary>The people the sample bookings belong to (all created by the demo seed).</summary>
    public sealed record People(Guid ClientA, Guid ClientB, Guid Photographer, Guid WebDeveloper, Guid Social, Guid Admin);

    /// <summary>Ensures every sample booking exists. Skips (with a log line) if the observers are not registered.</summary>
    public static async Task SeedAsync(ExsensicDbContext db, BookingEventDispatcher? dispatcher, People people,
        TimeProvider timeProvider, ILogger logger, CancellationToken ct)
    {
        if (dispatcher is null)
        {
            logger.LogWarning("Booking observers are not registered, so sample bookings were not added.");
            return;
        }

        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.ToOffset(Sast).DateTime);
        var services = await db.Services.ToListAsync(ct);
        Service Svc(ServiceCategory category, string nameStartsWith) =>
            services.First(s => s.Category == category && s.Name.StartsWith(nameStartsWith, StringComparison.Ordinal));

        var photoshoot = Svc(ServiceCategory.Photoshoot, "Professional");
        var websiteDesign = Svc(ServiceCategory.Website, "Website Design");
        var websiteDemo = Svc(ServiceCategory.Website, "Website Demonstration");
        var instagram = Svc(ServiceCategory.Instagram, "Instagram");
        var tiktok = Svc(ServiceCategory.TikTok, "TikTok");

        var photoshootBrief = new Dictionary<string, string>
        {
            ["shootType"] = "Product", ["location"] = "Studio", ["quantity"] = "24",
            ["description"] = "Catalogue photos of our new spring range on a white background for the online store.",
        };

        // Two waiting for approval.
        await EnsureAsync(db, dispatcher, "EXS-DEMO-01", people.ClientA, photoshoot, () => FreeSlotAsync(db, photoshoot, today.AddDays(3), ct),
            photoshootBrief, booking => { }, now, ct);
        await EnsureAsync(db, dispatcher, "EXS-DEMO-02", people.ClientB, websiteDesign, () => FreeSlotAsync(db, websiteDesign, today.AddDays(4), ct),
            new() { ["projectType"] = "Redesign", ["currentWebsite"] = "https://dlamini.design", ["pageCount"] = "8",
                    ["description"] = "Refresh our portfolio site so it works well on phones and shows recent projects." },
            booking => { }, now, ct);

        // Two approved and assigned to qualified staff.
        await EnsureAsync(db, dispatcher, "EXS-DEMO-03", people.ClientA, instagram, () => FreeSlotAsync(db, instagram, today.AddDays(5), ct),
            new() { ["accountHandle"] = "nkositraders", ["focus"] = "Growth",
                    ["description"] = "Grow followers for our retail store and plan three months of posts." },
            booking => booking.Confirm(people.Social, people.Admin, now), now, ct);
        await EnsureAsync(db, dispatcher, "EXS-DEMO-04", people.ClientB, photoshoot, () => FreeSlotAsync(db, photoshoot, today.AddDays(6), ct),
            new() { ["shootType"] = "Team", ["location"] = "On location", ["quantity"] = "6",
                    ["description"] = "Headshots and a group photo of our design team at our office." },
            booking => booking.Confirm(people.Photographer, people.Admin, now), now, ct);

        // One delivered last week.
        await EnsureAsync(db, dispatcher, "EXS-DEMO-05", people.ClientA, websiteDemo,
            () => SlotAtAsync(db, LastWeekday(today.AddDays(-7)), new TimeOnly(9, 0), new TimeOnly(11, 0), ct),
            new() { ["projectType"] = "Demonstration",
                    ["description"] = "Walk us through an online store you built so we can compare options." },
            booking =>
            {
                booking.Confirm(people.WebDeveloper, people.Admin, now);
                booking.Complete(StartUtc(booking.TimeSlot!), people.WebDeveloper, now);
            }, now, ct);

        // One rejected by the admin.
        await EnsureAsync(db, dispatcher, "EXS-DEMO-06", people.ClientB, tiktok, () => FreeSlotAsync(db, tiktok, today.AddDays(7), ct),
            new() { ["accountHandle"] = "dlaminidesign", ["focus"] = "Content ideas",
                    ["description"] = "Ideas for short videos that show our design process." },
            booking => booking.Reject("The requested week is fully booked. Please choose another date.", people.Admin, now), now, ct);

        // Every day: one confirmed photographer booking that has already started, for "staff completes" in the
        // demo and the end-to-end test. Once it has been completed, the next start-up (every deploy) adds
        // another one (EXS-TODAY-yyMMdd-2, -3, …) so the step can be shown again the same day.
        var todayPrefix = $"EXS-TODAY-{today:yyMMdd}";
        var todays = await db.Bookings.Where(b => b.Reference.StartsWith(todayPrefix)).Select(b => b.Status).ToListAsync(ct);
        var todayReference = todays.Count == 0 ? todayPrefix : $"{todayPrefix}-{todays.Count + 1}";
        if (todays.Contains(BookingStatus.Confirmed)) todayReference = todayPrefix;   // one is still waiting: EnsureAsync skips
        var start = StartedEarlierToday(now);
        await EnsureAsync(db, dispatcher, todayReference, people.ClientA, photoshoot,
            () => SlotAtAsync(db, today, start, start.AddHours(2) > start ? start.AddHours(2) : new TimeOnly(23, 59), ct),
            photoshootBrief, booking => booking.Confirm(people.Photographer, people.Admin, now), now, ct);

        logger.LogInformation("Sample bookings are in place.");
    }

    /// <summary>Creates one booking (if its reference is new), applies the follow-up actions, and records every event.</summary>
    private static async Task EnsureAsync(ExsensicDbContext db, BookingEventDispatcher dispatcher, string reference, Guid clientId,
        Service service, Func<Task<TimeSlot?>> findSlot, Dictionary<string, string> requirements, Action<Booking> then,
        DateTimeOffset now, CancellationToken ct)
    {
        if (await db.Bookings.AnyAsync(b => b.Reference == reference, ct)) return;
        if (await findSlot() is not { } slot) return;

        var booking = Booking.Create(clientId, service, slot, reference, now);
        foreach (var (key, value) in requirements)
        {
            booking.Requirements.Add(new BookingRequirement { FieldKey = key, FieldValue = value });
        }

        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);   // gives the booking its id, which notifications link to

        then(booking);
        await dispatcher.DispatchAsync(booking, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The first future slot from <paramref name="from"/> that is long enough for the service and has no active booking.</summary>
    private static async Task<TimeSlot?> FreeSlotAsync(ExsensicDbContext db, Service service, DateOnly from, CancellationToken ct)
    {
        var candidates = await db.TimeSlots
            .Where(t => t.SlotDate >= from && !t.IsBlocked)
            .Where(t => !db.Bookings.Any(b => b.TimeSlotId == t.Id
                && (b.Status == BookingStatus.Requested || b.Status == BookingStatus.Confirmed)))
            .OrderBy(t => t.SlotDate).ThenBy(t => t.StartTime)
            .Take(30)
            .ToListAsync(ct);
        return candidates.FirstOrDefault(t => (t.EndTime - t.StartTime).TotalMinutes >= service.DurationMinutes);
    }

    /// <summary>The slot at an exact date and time, created if it does not exist (used for past and in-progress slots).</summary>
    private static async Task<TimeSlot?> SlotAtAsync(ExsensicDbContext db, DateOnly date, TimeOnly start, TimeOnly end, CancellationToken ct)
    {
        var slot = await db.TimeSlots.FirstOrDefaultAsync(t => t.SlotDate == date && t.StartTime == start, ct);
        if (slot is null)
        {
            slot = new TimeSlot { SlotDate = date, StartTime = start, EndTime = end };
            db.TimeSlots.Add(slot);
            await db.SaveChangesAsync(ct);
        }

        var taken = await db.Bookings.AnyAsync(b => b.TimeSlotId == slot.Id
            && (b.Status == BookingStatus.Requested || b.Status == BookingStatus.Confirmed), ct);
        return taken ? null : slot;
    }

    /// <summary>A start time one hour before now (SAST), on the hour, so the slot has clearly begun.</summary>
    private static TimeOnly StartedEarlierToday(DateTimeOffset nowUtc)
    {
        var nowSast = nowUtc.ToOffset(Sast);
        return nowSast.Hour == 0 ? new TimeOnly(0, 0) : new TimeOnly(nowSast.Hour - 1, 0);
    }

    private static DateOnly LastWeekday(DateOnly date) =>
        date.DayOfWeek switch { DayOfWeek.Saturday => date.AddDays(-1), DayOfWeek.Sunday => date.AddDays(-2), _ => date };

    /// <summary>A slot's start as UTC (slots are stored in SAST), as Booking.Complete expects.</summary>
    private static DateTimeOffset StartUtc(TimeSlot slot) =>
        new DateTimeOffset(slot.SlotDate.ToDateTime(slot.StartTime), Sast).ToUniversalTime();
}
