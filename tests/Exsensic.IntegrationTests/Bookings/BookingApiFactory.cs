using System.Net.Http.Headers;
using System.Security.Cryptography;
using Exsensic.Api.Security;
using Exsensic.Contracts.Auth;
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
    /// Creates a user in the given role and returns an HTTP client signed in as them, plus their id.
    /// The client uses https, so the API's HTTPS redirection doesn't get in the way.
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

        var token = scope.ServiceProvider.GetRequiredService<TokenService>().CreateFor(user, role);
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        return (client, user.Id);
    }

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
    public async Task<int> AddFutureSlotAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExsensicDbContext>();
        var todaySast = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(SastTime.Offset).DateTime);
        var slot = new TimeSlot
        {
            SlotDate = todaySast.AddDays(10 + Interlocked.Increment(ref _nextSlotDay)),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(11, 0),
        };
        db.TimeSlots.Add(slot);
        await db.SaveChangesAsync();
        return slot.Id;
    }

    /// <summary>A fresh database context for checking what the API saved. Dispose the scope after use.</summary>
    public AsyncServiceScope NewScope() => Services.CreateAsyncScope();
}
