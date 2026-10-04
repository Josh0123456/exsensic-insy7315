using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Enums;
using Exsensic.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Exsensic.Data.Seed;

public static class DatabaseInitialiser
{
    public static async Task InitialiseAsync(
        IServiceProvider services,
        bool seedDemoData,
        CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExsensicDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ExsensicDbContext>>();

        await db.Database.MigrateAsync(ct);

        foreach (var role in new[] { RoleNames.Client, RoleNames.Staff, RoleNames.Admin })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        var adminEmail = config["Seed:AdminEmail"];
        var adminPassword = config["Seed:AdminPassword"];
        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var existing = await userManager.FindByEmailAsync(adminEmail);
            if (existing is null)
            {
                var admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Admin",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };
                var result = await userManager.CreateAsync(admin, adminPassword);
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(admin, RoleNames.Admin);
                else
                    logger.LogWarning("Admin seed failed: {Errors}",
                        string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        if (seedDemoData)
            await SeedDemoDataAsync(db, userManager, config, logger, ct);
    }

    private static async Task SeedDemoDataAsync(
        ExsensicDbContext db,
        UserManager<ApplicationUser> userManager,
        IConfiguration config,
        ILogger logger,
        CancellationToken ct)
    {
        var demoPassword = config["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(demoPassword))
        {
            logger.LogWarning("Seed:DemoPassword missing. Skipping demo data.");
            return;
        }

        var photographer = await EnsureUserAsync(userManager, "photo@exsensic.local",
            "Sipho Photographer", RoleNames.Staff, demoPassword, logger);
        var webDev = await EnsureUserAsync(userManager, "web@exsensic.local",
            "Lerato Web", RoleNames.Staff, demoPassword, logger);
        var social = await EnsureUserAsync(userManager, "social@exsensic.local",
            "Mandla Social", RoleNames.Staff, demoPassword, logger);

        if (photographer is null || webDev is null || social is null) return;

        if (!await db.StaffProfiles.AnyAsync(p => p.UserId == photographer.Id, ct))
            db.StaffProfiles.Add(new StaffProfile { UserId = photographer.Id, JobTitle = "Photographer" });
        if (!await db.StaffProfiles.AnyAsync(p => p.UserId == webDev.Id, ct))
            db.StaffProfiles.Add(new StaffProfile { UserId = webDev.Id, JobTitle = "Web Developer" });
        if (!await db.StaffProfiles.AnyAsync(p => p.UserId == social.Id, ct))
            db.StaffProfiles.Add(new StaffProfile { UserId = social.Id, JobTitle = "Social Media Specialist" });

        // Clients.
        var clientA = await EnsureUserAsync(userManager, "client.a@example.com",
            "Thabo Nkosi", RoleNames.Client, demoPassword, logger);
        var clientB = await EnsureUserAsync(userManager, "client.b@example.com",
            "Zanele Dlamini", RoleNames.Client, demoPassword, logger);

        if (clientA is not null && !await db.ClientProfiles.AnyAsync(p => p.UserId == clientA.Id, ct))
            db.ClientProfiles.Add(new ClientProfile
            {
                UserId = clientA.Id,
                CompanyName = "Nkosi Traders",
                Phone = "0115550101"
            });
        if (clientB is not null && !await db.ClientProfiles.AnyAsync(p => p.UserId == clientB.Id, ct))
            db.ClientProfiles.Add(new ClientProfile
            {
                UserId = clientB.Id,
                CompanyName = "Dlamini Design",
                Phone = "0115550102"
            });

        await db.SaveChangesAsync(ct);

        // Services.
        var services = new[]
        {
            new Service
            {
                Name = "Professional Product Photoshoot",
                Category = ServiceCategory.Photoshoot,
                Description = "Studio or on-location shoot for your products or brand.",
                DurationMinutes = 120,
                BasePrice = 2500m
            },
            new Service
            {
                Name = "Website Design Consultation",
                Category = ServiceCategory.Website,
                Description = "Discuss your website requirements with our team.",
                DurationMinutes = 60,
                BasePrice = null
            },
            new Service
            {
                Name = "Website Demonstration",
                Category = ServiceCategory.Website,
                Description = "A walkthrough of a website we've built.",
                DurationMinutes = 45,
                BasePrice = null
            },
            new Service
            {
                Name = "Instagram Account Consultation",
                Category = ServiceCategory.Instagram,
                Description = "Audit and strategy for your Instagram presence.",
                DurationMinutes = 60,
                BasePrice = 800m
            },
            new Service
            {
                Name = "TikTok Content Consultation",
                Category = ServiceCategory.TikTok,
                Description = "Plan and content strategy for TikTok.",
                DurationMinutes = 60,
                BasePrice = 800m
            }
        };

        foreach (var svc in services)
        {
            if (!await db.Services.AnyAsync(s => s.Name == svc.Name, ct))
                db.Services.Add(svc);
        }

        await db.SaveChangesAsync(ct);

        // StaffService links.
        var savedServices = await db.Services.ToListAsync(ct);
        var photoSvc = savedServices.First(s => s.Name.Contains("Photoshoot"));
        var webSvcA = savedServices.First(s => s.Name.Contains("Website Design"));
        var webSvcB = savedServices.First(s => s.Name.Contains("Website Demonstration"));
        var igSvc = savedServices.First(s => s.Name.Contains("Instagram"));
        var ttSvc = savedServices.First(s => s.Name.Contains("TikTok"));

        await EnsureStaffServiceAsync(db, photographer.Id, photoSvc.Id, ct);
        await EnsureStaffServiceAsync(db, webDev.Id, webSvcA.Id, ct);
        await EnsureStaffServiceAsync(db, webDev.Id, webSvcB.Id, ct);
        await EnsureStaffServiceAsync(db, social.Id, igSvc.Id, ct);
        await EnsureStaffServiceAsync(db, social.Id, ttSvc.Id, ct);

        await db.SaveChangesAsync(ct);

        // Time slots for the next 30 weekdays. Three windows per day.
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(2));
        var windows = new[]
        {
            (new TimeOnly(9, 0), new TimeOnly(11, 0)),
            (new TimeOnly(12, 0), new TimeOnly(13, 0)),
            (new TimeOnly(14, 0), new TimeOnly(16, 0))
        };

        var daysAdded = 0;
        for (var i = 0; i < 60 && daysAdded < 30; i++)
        {
            var date = today.AddDays(i);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;

            foreach (var (start, end) in windows)
            {
                var exists = await db.TimeSlots.AnyAsync(t =>
                    t.SlotDate == date && t.StartTime == start, ct);
                if (!exists)
                    db.TimeSlots.Add(new TimeSlot { SlotDate = date, StartTime = start, EndTime = end });
            }
            daysAdded++;
        }

        await db.SaveChangesAsync(ct);

        await SeedDemoBookingsAsync(db, userManager, config, logger, ct);

        logger.LogInformation("Demo seed complete.");
    }

    private static async Task SeedDemoBookingsAsync(
        ExsensicDbContext db,
        UserManager<ApplicationUser> userManager,
        IConfiguration config,
        ILogger logger,
        CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var nowSast = nowUtc.ToOffset(TimeSpan.FromHours(2));
        var todaySast = DateOnly.FromDateTime(nowSast.DateTime);
        

        var clientA = await userManager.FindByEmailAsync("client.a@example.com");
        var clientB = await userManager.FindByEmailAsync("client.b@example.com");
        var photographer = await userManager.FindByEmailAsync("photo@exsensic.local");
        var webDev = await userManager.FindByEmailAsync("web@exsensic.local");

        var adminEmail = config["Seed:AdminEmail"];
        var admin = string.IsNullOrWhiteSpace(adminEmail)
            ? null
            : await userManager.FindByEmailAsync(adminEmail);

        if (clientA is null || clientB is null || photographer is null || webDev is null || admin is null)
        {
            logger.LogWarning("Demo booking seed skipped: one or more seed users are missing.");
            return;
        }

        var photoSvc = await db.Services.FirstOrDefaultAsync(s => s.Name.Contains("Photoshoot"), ct);
        var webSvc = await db.Services.FirstOrDefaultAsync(s => s.Name.Contains("Website Design"), ct);
        var igSvc = await db.Services.FirstOrDefaultAsync(s => s.Name.Contains("Instagram"), ct);
        if (photoSvc is null || webSvc is null || igSvc is null)
        {
            logger.LogWarning("Demo booking seed skipped: services not found.");
            return;
        }

        var futureSlots = await db.TimeSlots
            .Where(t => t.SlotDate > todaySast)
            .OrderBy(t => t.SlotDate).ThenBy(t => t.StartTime)
            .Take(5)
            .ToListAsync(ct);
        if (futureSlots.Count < 5)
        {
            logger.LogWarning("Demo booking seed skipped: fewer than five future slots available.");
            return;
        }

        var weekAgo = todaySast.AddDays(-7);
        var weekAgoSlot = await EnsureSlotAsync(db, weekAgo, new TimeOnly(9, 0), new TimeOnly(11, 0), ct);

        var startedHour = Math.Clamp(nowSast.Hour - 1, 0, 22);
        var startedSlot = await EnsureSlotAsync(
            db, todaySast, new TimeOnly(startedHour, 0), new TimeOnly(startedHour + 1, 0), ct);

        // Two Requested.
        await AddDemoBookingIfMissing(db,
            "EXS-DEMORQAAA", clientA.Id, photoSvc.Id, futureSlots[0].Id,
            BookingStatus.Requested, null, clientA.Id,
            nowUtc.AddDays(-2), null,
            new[]
            {
                ("shootType", "Product"),
                ("location", "Studio"),
                ("quantity", "10"),
                ("description", "Product shots for the winter catalogue. Online store only."),
            }, ct);

        await AddDemoBookingIfMissing(db,
            "EXS-DEMORQBBB", clientB.Id, igSvc.Id, futureSlots[1].Id,
            BookingStatus.Requested, null, clientB.Id,
            nowUtc.AddDays(-1), null,
            new[]
            {
                ("accountHandle", "@dlaminidesign"),
                ("focus", "Growth"),
                ("description", "Growing the account to reach a wider design client base."),
            }, ct);

        await AddDemoBookingIfMissing(db,
            "EXS-DEMOCFAAA", clientA.Id, photoSvc.Id, futureSlots[2].Id,
            BookingStatus.Confirmed, photographer.Id, admin.Id,
            nowUtc.AddDays(-5), null,
            new[]
            {
                ("shootType", "Team"),
                ("location", "On location"),
                ("quantity", "6"),
                ("description", "Team headshots for the new website."),
            }, ct);

        await AddDemoBookingIfMissing(db,
            "EXS-DEMOCFBBB", clientB.Id, webSvc.Id, futureSlots[3].Id,
            BookingStatus.Confirmed, webDev.Id, admin.Id,
            nowUtc.AddDays(-4), null,
            new[]
            {
                ("projectType", "New website"),
                ("pageCount", "8"),
                ("description", "Simple brochure site for our design studio."),
            }, ct);

        await AddDemoBookingIfMissing(db,
            "EXS-DEMOCOAAA", clientA.Id, photoSvc.Id, weekAgoSlot.Id,
            BookingStatus.Completed, photographer.Id, admin.Id,
            nowUtc.AddDays(-14), null,
            new[]
            {
                ("shootType", "Product"),
                ("location", "Studio"),
                ("quantity", "20"),
                ("description", "Catalogue photos from last quarter."),
            }, ct);

        await AddDemoBookingIfMissing(db,
            "EXS-DEMOCNAAA", clientB.Id, webSvc.Id, futureSlots[4].Id,
            BookingStatus.Cancelled, null, admin.Id,
            nowUtc.AddDays(-3), "Rejected: Dates clash with another booking.",
            new[]
            {
                ("projectType", "Redesign"),
                ("pageCount", "5"),
                ("description", "Small redesign of an existing brochure site."),
            }, ct);

        await AddDemoBookingIfMissing(db,
            "EXS-DEMOSTAAA", clientA.Id, photoSvc.Id, startedSlot.Id,
            BookingStatus.Confirmed, photographer.Id, admin.Id,
            nowUtc.AddDays(-2), null,
            new[]
            {
                ("shootType", "Product"),
                ("location", "Studio"),
                ("quantity", "3"),
                ("description", "Quick product shots for a social post."),
            }, ct);

        logger.LogInformation("Demo booking seed complete.");
    }

    private static async Task<TimeSlot> EnsureSlotAsync(
        ExsensicDbContext db,
        DateOnly date,
        TimeOnly start,
        TimeOnly end,
        CancellationToken ct)
    {
        var existing = await db.TimeSlots.FirstOrDefaultAsync(
            t => t.SlotDate == date && t.StartTime == start, ct);
        if (existing is not null)
        {
            return existing;
        }

        var slot = new TimeSlot { SlotDate = date, StartTime = start, EndTime = end };
        db.TimeSlots.Add(slot);
        await db.SaveChangesAsync(ct);
        return slot;
    }

    private static async Task AddDemoBookingIfMissing(
        ExsensicDbContext db,
        string reference,
        Guid clientId,
        int serviceId,
        int timeSlotId,
        BookingStatus status,
        Guid? staffUserId,
        Guid approverId,
        DateTimeOffset createdAtUtc,
        string? cancellationReason,
        (string Key, string Value)[] requirements,
        CancellationToken ct)
    {
        if (await db.Bookings.AnyAsync(b => b.Reference == reference, ct))
        {
            return;
        }

        var booking = new Booking
        {
            Reference = reference,
            ClientUserId = clientId,
            ServiceId = serviceId,
            TimeSlotId = timeSlotId,
            StaffUserId = staffUserId,
            Status = status,
            CancellationReason = cancellationReason,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
        };

        foreach (var (key, value) in requirements)
        {
            booking.Requirements.Add(new BookingRequirement { FieldKey = key, FieldValue = value });
        }

        var confirmed = createdAtUtc.AddHours(20);
        var finished = createdAtUtc.AddDays(2);

        switch (status)
        {
            case BookingStatus.Requested:
                booking.StatusHistory.Add(HistoryRow(null, BookingStatus.Requested, clientId, createdAtUtc, null));
                booking.UpdatedAtUtc = createdAtUtc;
                break;

            case BookingStatus.Confirmed:
                booking.StatusHistory.Add(HistoryRow(null, BookingStatus.Requested, clientId, createdAtUtc, null));
                booking.StatusHistory.Add(HistoryRow(BookingStatus.Requested, BookingStatus.Confirmed, approverId, confirmed, null));
                booking.UpdatedAtUtc = confirmed;
                break;

            case BookingStatus.Completed:
                booking.StatusHistory.Add(HistoryRow(null, BookingStatus.Requested, clientId, createdAtUtc, null));
                booking.StatusHistory.Add(HistoryRow(BookingStatus.Requested, BookingStatus.Confirmed, approverId, confirmed, null));
                booking.StatusHistory.Add(HistoryRow(BookingStatus.Confirmed, BookingStatus.Completed, staffUserId ?? approverId, finished, null));
                booking.UpdatedAtUtc = finished;
                break;

            case BookingStatus.Cancelled:
                booking.StatusHistory.Add(HistoryRow(null, BookingStatus.Requested, clientId, createdAtUtc, null));
                booking.StatusHistory.Add(HistoryRow(BookingStatus.Requested, BookingStatus.Cancelled, approverId, confirmed, cancellationReason));
                booking.UpdatedAtUtc = confirmed;
                break;
        }

        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);

        static BookingStatusHistory HistoryRow(
            BookingStatus? from, BookingStatus to, Guid by, DateTimeOffset when, string? note) =>
            new()
            {
                FromStatus = from,
                ToStatus = to,
                ChangedByUserId = by,
                ChangedAtUtc = when,
                Note = note,
            };
    }

    private static async Task<ApplicationUser?> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string role,
        string password,
        ILogger logger)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null) return user;

        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            logger.LogWarning("Seed user {Email} failed: {Errors}",
                email, string.Join(", ", result.Errors.Select(e => e.Description)));
            return null;
        }

        await userManager.AddToRoleAsync(user, role);
        return user;
    }

    private static async Task EnsureStaffServiceAsync(
        ExsensicDbContext db, Guid staffId, int serviceId, CancellationToken ct)
    {
        if (!await db.StaffServices.AnyAsync(s =>
            s.StaffUserId == staffId && s.ServiceId == serviceId, ct))
        {
            db.StaffServices.Add(new StaffService { StaffUserId = staffId, ServiceId = serviceId });
        }
    }
}
