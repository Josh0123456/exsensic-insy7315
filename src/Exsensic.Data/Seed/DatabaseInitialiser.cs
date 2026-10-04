using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Enums;
using Exsensic.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Exsensic.Data.Seed;


/// Applies migrations, ensures roles and the first admin, and idempotently seeds demo data.
/// Called at startup in Development and Staging only.

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
            await SeedDemoDataAsync(db, userManager, config, logger, scope.ServiceProvider, ct);
    }

    private static async Task SeedDemoDataAsync(
        ExsensicDbContext db,
        UserManager<ApplicationUser> userManager,
        IConfiguration config,
        ILogger logger,
        IServiceProvider serviceProvider,
        CancellationToken ct)
    {
        var demoPassword = config["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(demoPassword))
        {
            logger.LogWarning("Seed:DemoPassword missing. Skipping demo data.");
            return;
        }

        // Staff. Check by email before inserting so restarts don't duplicate.
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

        // Services. Matches the prototype list from Part 1.
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

        // StaffService links. Now that services have IDs, wire them up.
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

        // Sample bookings in every status (Josh, taking over P3 step 16), through the real booking rules.
        var admin = config["Seed:AdminEmail"] is { Length: > 0 } adminEmail ? await userManager.FindByEmailAsync(adminEmail) : null;
        if (clientA is not null && clientB is not null && admin is not null)
        {
            await DemoBookingSeeder.SeedAsync(db, serviceProvider.GetService<Exsensic.Core.Bookings.Observers.BookingEventDispatcher>(),
                new DemoBookingSeeder.People(clientA.Id, clientB.Id, photographer.Id, webDev.Id, social.Id, admin.Id),
                serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System, logger, ct);
        }

        logger.LogInformation("Demo seed complete.");
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
