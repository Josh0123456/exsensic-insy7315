using Exsensic.Contracts.Enums;
using Exsensic.Core.Abstractions;
using Exsensic.Core.Bookings;
using Exsensic.Core.Entities;
using Exsensic.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Exsensic.IntegrationTests.Bookings;

/// <summary>
/// One throwaway SQL Server database shared by a booking test class, built from the committed migrations
/// and wired exactly like the API (same DbContext settings and booking services), with a fixed clock.
/// Uses ConnectionStrings:Tests in CI, or LocalDB on a developer machine. Each test adds its own service
/// and slots, so tests don't depend on each other or on the order they run in.
/// </summary>
public sealed class BookingDatabaseFixture : IAsyncLifetime
{
    /// <summary>"Now" for every booking test: Saturday 3 October 2026, 10:00 SAST.</summary>
    public static readonly DateTimeOffset NowUtc = new(2026, 10, 3, 8, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _services;
    private int _nextServiceNumber;
    private int _nextSlotDay;

    /// <summary>Configures the services on a uniquely named test database.</summary>
    public BookingDatabaseFixture()
    {
        var baseConnection = Environment.GetEnvironmentVariable("ConnectionStrings__Tests")
            ?? "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";
        var connection = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = $"ExsensicBookings_{Guid.NewGuid():N}" };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = connection.ConnectionString })
            .Build();

        _services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<TimeProvider>(new FixedTimeProvider(NowUtc))
            .AddExsensicData(configuration)
            .AddExsensicBookings()
            .BuildServiceProvider();
    }

    /// <summary>Creates the test database from the committed migrations.</summary>
    public async Task InitializeAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ExsensicDbContext>().Database.MigrateAsync();
    }

    /// <summary>Deletes the test database.</summary>
    public async Task DisposeAsync()
    {
        await using (var scope = _services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ExsensicDbContext>().Database.EnsureDeletedAsync();
        }

        await _services.DisposeAsync();
    }

    /// <summary>A new dependency-injection scope, like one HTTP request. Dispose it after use.</summary>
    public AsyncServiceScope NewScope() => _services.CreateAsyncScope();

    /// <summary>A fresh context for checking what was saved, with nothing cached from the code under test.</summary>
    public ExsensicDbContext NewContext() => _services.CreateScope().ServiceProvider.GetRequiredService<ExsensicDbContext>();

    /// <summary>Adds an active Photoshoot service (120 minutes) with a unique name and returns its id.</summary>
    public async Task<int> AddServiceAsync(int durationMinutes = 120, bool isActive = true)
    {
        var service = new Service
        {
            Name = $"Test photoshoot {Interlocked.Increment(ref _nextServiceNumber)}",
            Category = ServiceCategory.Photoshoot,
            Description = "Created by a booking test.",
            DurationMinutes = durationMinutes,
            IsActive = isActive,
        };

        await using var db = NewContext();
        db.Services.Add(service);
        await db.SaveChangesAsync();
        return service.Id;
    }

    /// <summary>
    /// Adds a 09:00–11:00 slot on its own future date (so the (date, start) unique index never clashes)
    /// and returns its id. A negative <paramref name="daysFromNow"/> puts it in the past.
    /// </summary>
    public async Task<int> AddSlotAsync(int? daysFromNow = null, bool isBlocked = false, int lengthMinutes = 120)
    {
        var days = daysFromNow ?? 7 + Interlocked.Increment(ref _nextSlotDay);
        var start = new TimeOnly(9, 0);
        var slot = new TimeSlot
        {
            SlotDate = DateOnly.FromDateTime(NowUtc.ToOffset(SastTime.Offset).DateTime).AddDays(days),
            StartTime = start,
            EndTime = start.AddMinutes(lengthMinutes),
            IsBlocked = isBlocked,
        };

        await using var db = NewContext();
        db.TimeSlots.Add(slot);
        await db.SaveChangesAsync();
        return slot.Id;
    }

    /// <summary>A clock that always returns the same time, so slot rules give the same result on every run.</summary>
    private sealed class FixedTimeProvider(DateTimeOffset nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nowUtc;
    }
}

/// <summary>Lets several test classes share one database fixture instance each.</summary>
[CollectionDefinition(Name)]
public sealed class BookingDatabaseCollection : ICollectionFixture<BookingDatabaseFixture>
{
    /// <summary>The collection name used by booking test classes.</summary>
    public const string Name = "Booking database";
}
