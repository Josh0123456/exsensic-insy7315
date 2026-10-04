using Exsensic.Core.Abstractions;
using Exsensic.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Exsensic.Data;

/// EF Core context. Inherits Identity so users and roles live in the same DB.

public class ExsensicDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IAppDbContext
{
    public ExsensicDbContext(DbContextOptions<ExsensicDbContext> options) : base(options) { }

    public DbSet<ClientProfile> ClientProfiles => Set<ClientProfile>();
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<StaffService> StaffServices => Set<StaffService>();
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingRequirement> BookingRequirements => Set<BookingRequirement>();
    public DbSet<BookingStatusHistory> BookingStatusHistory => Set<BookingStatusHistory>();
    public DbSet<Notification> Notifications => Set<Notification>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)
        => Database.BeginTransactionAsync(ct);

    /// <summary>
    /// Runs the work inside EF Core's execution strategy, which is required for our own transactions
    /// when retry-on-failure is on. A retry starts from a clean change tracker so nothing from the failed
    /// attempt is saved twice.
    /// </summary>
    public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        var attempt = 0;
        return Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) ChangeTracker.Clear();

            await using var transaction = await Database.BeginTransactionAsync(ct);
            var result = await work(ct);
            await transaction.CommitAsync(ct);
            return result;
        });
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ExsensicDbContext).Assembly);
    }
}
