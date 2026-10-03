using System;
using System.Collections.Generic;
using System.Text;
using Exsensic.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Exsensic.Core.Abstractions;

public interface IAppDbContext
{
    DbSet<ClientProfile> ClientProfiles { get; }
    DbSet<StaffProfile> StaffProfiles { get; }
    DbSet<Service> Services { get; }
    DbSet<StaffService> StaffServices { get; }
    DbSet<TimeSlot> TimeSlots { get; }
    DbSet<Booking> Bookings { get; }
    DbSet<BookingRequirement> BookingRequirements { get; }
    DbSet<BookingStatusHistory> BookingStatusHistory { get; }
    DbSet<Notification> Notifications { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    /// <summary>
    /// Do not use for new code: with retry-on-failure switched on, EF Core rejects transactions started
    /// this way. Use <see cref="InTransactionAsync{T}"/> instead.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs <paramref name="work"/> in one database transaction and commits it. If the connection drops
    /// briefly (for example while the free Azure SQL database resumes), the whole block is retried from
    /// the start, so do all loading and changes inside it. Errors such as a unique-index clash are not
    /// retried and reach the caller unchanged.
    /// </summary>
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default);
}
