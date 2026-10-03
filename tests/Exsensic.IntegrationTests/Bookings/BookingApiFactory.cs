using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Exsensic.Api.Security;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Enums;
using Exsensic.Core.Bookings;
using Exsensic.Core.Entities;
using Exsensic.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Exsensic.IntegrationTests.Bookings;

/// <summary>
/// Runs the real API in memory (WebApplicationFactory) against a throwaway SQL Server database, so booking
/// tests go through routing, authentication, authorisation, rate limiting and error handling exactly as a
/// browser request would. Uses ConnectionStrings:Tests in CI, or LocalDB on a developer machine.
/// </summary>
/// <remarks>
/// Test users are created directly through Identity and given a token, rather than through
/// POST /auth/register, because registration is rate limited to 3 per minute per IP. Their passwords are
/// never used, and the signing key is random per test run, so no secret is stored in the code.
/// </remarks>
public sealed class BookingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _connectionString;
    private int _nextServiceNumber;
    private int _nextSlotDay;
    private int _nextCompanyNumber;
    private int _nextSoonMinute;

    /// <summary>Picks a uniquely named test database.</summary>
    public BookingApiFactory()
    {
        var baseConnection = Environment.GetEnvironmentVariable("ConnectionStrings__Tests")
            ?? "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";
        _connectionString = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = $"ExsensicApi_{Guid.NewGuid():N}" }.ConnectionString;
    }

    /// <summary>
    /// Points the API at the test database and gives it test-only JWT settings. "Testing" is not
    /// Development, so the API runs with its production checks, and MigrateOnStartup builds the database
    /// (roles included) from the committed migrations without demo data.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Jwt:Issuer", "exsensic-tests");
        builder.UseSetting("Jwt:Audience", "exsensic-tests");
        builder.UseSetting("Jwt:LifetimeMinutes", "60");
        builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
    }

    /// <summary>Starts the API, which migrates the test database.</summary>
    public Task InitializeAsync()
    {
        _ = Server;
        return Task.CompletedTask;
    }

    /// <summary>Deletes the test database and stops the API.</summary>
    async Task IAsyncLifetime.DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ExsensicDbContext>().Database.EnsureDeletedAsync();
        }

        await DisposeAsync();
    }

    /// <summary>
    /// Creates a user in the given role (with a client or staff profile) and returns an HTTP client signed in
    /// as them, plus their id.
    /// </summary>
    public async Task<(HttpClient Client, Guid UserId)> CreateSignedInClientAsync(string role = RoleNames.Client)
    {
        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var email = $"test-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = $"Test {role}",
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);

        if (role == RoleNames.Client)
        {
            var db = scope.ServiceProvider.GetRequiredService<ExsensicDbContext>();
            db.ClientProfiles.Add(new ClientProfile { UserId = user.Id, CompanyName = $"Test Company {Interlocked.Increment(ref _nextCompanyNumber)}", Phone = "0100000000" });
            await db.SaveChangesAsync();
        }
        else if (role == RoleNames.Staff)
        {
            var db = scope.ServiceProvider.GetRequiredService<ExsensicDbContext>();
            db.StaffProfiles.Add(new StaffProfile { UserId = user.Id, JobTitle = "Photographer" });
            await db.SaveChangesAsync();
        }

        var token = scope.ServiceProvider.GetRequiredService<TokenService>().CreateFor(user, role);
        var client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        return (client, user.Id);
    }

    /// <summary>An HTTP client with nobody signed in. It uses https, so the API's HTTPS redirection doesn't get in the way.</summary>
    public HttpClient CreateAnonymousClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    /// <summary>Adds an active 120-minute Photoshoot service with a unique name and returns its id.</summary>
    public async Task<int> AddServiceAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExsensicDbContext>();
        var service = new Service
        {
            Name = $"API test photoshoot {Interlocked.Increment(ref _nextServiceNumber)}",
            Category = ServiceCategory.Photoshoot,
            Description = "Created by a booking API test.",
            DurationMinutes = 120,
            IsActive = true,
        };
        db.Services.Add(service);
        await db.SaveChangesAsync();
        return service.Id;
    }

    /// <summary>Adds a 09:00–11:00 slot on its own date, at least ten days in the future, and returns its id.</summary>
    public Task<int> AddFutureSlotAsync() => AddSlotAsync(NextFutureDate(), new TimeOnly(9, 0));

    /// <summary>A date (SAST) at least ten days ahead that no other test in this run has used.</summary>
    public DateOnly NextFutureDate() =>
        TodaySast().AddDays(10 + Interlocked.Increment(ref _nextSlotDay));

    /// <summary>Adds a slot on the given date and start time (SAST) and returns its id.</summary>
    public async Task<int> AddSlotAsync(DateOnly date, TimeOnly start, int lengthMinutes = 120)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExsensicDbContext>();
        var slot = new TimeSlot { SlotDate = date, StartTime = start, EndTime = start.AddMinutes(lengthMinutes) };
        db.TimeSlots.Add(slot);
        await db.SaveChangesAsync();
        return slot.Id;
    }

    /// <summary>
    /// Adds a two-hour slot that starts about three hours from now, inside the 24-hour cancellation cut-off.
    /// Late in the evening it moves to 01:00 the next day, so the slot never runs past midnight.
    /// </summary>
    public Task<int> AddSlotStartingSoonAsync()
    {
        var soon = DateTimeOffset.UtcNow.ToOffset(SastTime.Offset).AddHours(3);
        var start = new DateTime(soon.Year, soon.Month, soon.Day, soon.Hour, 0, 0);
        if (start.Hour >= 22)
        {
            start = start.Date.AddDays(1).AddHours(1);
        }

        // A different minute per call keeps the (date, start time) pair unique.
        start = start.AddMinutes(Interlocked.Increment(ref _nextSoonMinute) % 60);
        return AddSlotAsync(DateOnly.FromDateTime(start), TimeOnly.FromDateTime(start));
    }

    /// <summary>Lets a staff member deliver a service (a StaffServices link).</summary>
    public async Task LinkStaffToServiceAsync(Guid staffUserId, int serviceId)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExsensicDbContext>();
        db.StaffServices.Add(new StaffService { StaffUserId = staffUserId, ServiceId = serviceId });
        await db.SaveChangesAsync();
    }

    /// <summary>Deactivates an account, as an admin would.</summary>
    public async Task DeactivateAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        user!.IsActive = false;
        Assert.True((await users.UpdateAsync(user)).Succeeded);
    }

    /// <summary>
    /// Books a Photoshoot through POST /bookings as the given client, on a new service and slot unless
    /// they are given, and returns the result.
    /// </summary>
    public async Task<BookingCreatedDto> BookAsync(HttpClient client, int? serviceId = null, int? slotId = null)
    {
        var request = new CreateBookingRequest(
            serviceId ?? await AddServiceAsync(),
            slotId ?? await AddFutureSlotAsync(),
            new Dictionary<string, string>
            {
                ["shootType"] = "Product",
                ["location"] = "Studio",
                ["quantity"] = "10",
                ["description"] = "Shots for the online store.",
            });

        var response = await client.PostAsJsonAsync("/api/v1/bookings", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookingCreatedDto>())!;
    }

    /// <summary>Reads a booking's details as the given user.</summary>
    public static async Task<BookingDetailDto> GetDetailAsync(HttpClient client, int bookingId) =>
        (await client.GetFromJsonAsync<BookingDetailDto>($"/api/v1/bookings/{bookingId}"))!;

    private static DateOnly TodaySast() =>
        DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(SastTime.Offset).DateTime);

    /// <summary>A fresh database context for checking what the API saved. Dispose the scope after use.</summary>
    public AsyncServiceScope NewScope() => Services.CreateAsyncScope();
}
