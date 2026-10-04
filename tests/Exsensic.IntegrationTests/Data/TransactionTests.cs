using Exsensic.Contracts.Enums;
using Exsensic.Core.Entities;
using Exsensic.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Exsensic.IntegrationTests.Data;

/// <summary>
/// Proves that transactions work with the API's real database settings (retry-on-failure switched on):
/// InTransactionAsync commits or rolls back as one unit, while the old BeginTransactionAsync is rejected.
/// Runs against ConnectionStrings:Tests (SQL Server in CI) or LocalDB, in a throwaway database.
/// </summary>
public sealed class TransactionTests : IAsyncLifetime
{
    private readonly ServiceProvider _services;

    /// <summary>Configures the DbContext exactly as the API does, on a uniquely named test database.</summary>
    public TransactionTests()
    {
        var baseConnection = Environment.GetEnvironmentVariable("ConnectionStrings__Tests")
            ?? "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";
        var connection = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = $"ExsensicTx_{Guid.NewGuid():N}" };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = connection.ConnectionString })
            .Build();
        _services = new ServiceCollection().AddExsensicData(configuration).BuildServiceProvider();
    }

    /// <summary>Creates the test database from the committed migrations.</summary>
    public async Task InitializeAsync() => await NewContext().Database.MigrateAsync();

    /// <summary>Deletes the test database.</summary>
    public async Task DisposeAsync()
    {
        await NewContext().Database.EnsureDeletedAsync();
        await _services.DisposeAsync();
    }

    /// <summary>Changes made inside the block are saved together.</summary>
    [Fact]
    public async Task InTransactionAsync_WorkSucceeds_CommitsChanges()
    {
        var db = NewContext();
        var id = await db.InTransactionAsync(async ct =>
        {
            var service = NewService("Committed service");
            db.Services.Add(service);
            await db.SaveChangesAsync(ct);
            return service.Id;
        });

        Assert.True(await NewContext().Services.AnyAsync(s => s.Id == id));
    }

    /// <summary>If the block throws after saving, nothing it saved is kept.</summary>
    [Fact]
    public async Task InTransactionAsync_WorkThrows_RollsBackChanges()
    {
        var db = NewContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.InTransactionAsync<int>(async ct =>
        {
            db.Services.Add(NewService("Rolled back service"));
            await db.SaveChangesAsync(ct);
            throw new InvalidOperationException("Simulated failure after saving.");
        }));

        Assert.False(await NewContext().Services.AnyAsync(s => s.Name == "Rolled back service"));
    }

    /// <summary>Documents why InTransactionAsync exists: EF rejects a hand-started transaction when retries are on.</summary>
    [Fact]
    public async Task BeginTransactionAsync_WithRetryOnFailure_IsRejected()
    {
        var db = NewContext();
        db.Services.Add(NewService("Never saved"));
        await using var transaction = await db.BeginTransactionAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private ExsensicDbContext NewContext() =>
        _services.CreateScope().ServiceProvider.GetRequiredService<ExsensicDbContext>();

    private static Service NewService(string name) => new()
    {
        Name = name,
        Category = ServiceCategory.Website,
        Description = "Created by a transaction test.",
        DurationMinutes = 60,
        IsActive = true,
    };
}
