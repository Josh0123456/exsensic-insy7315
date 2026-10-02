using Exsensic.Core.Abstractions;
using Exsensic.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ExsensicDbContext).Assembly);
    }
}
