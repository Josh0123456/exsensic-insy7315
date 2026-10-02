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
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
}
