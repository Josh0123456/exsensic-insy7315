using System;
using System.Collections.Generic;
using System.Text;
using Exsensic.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exsensic.Data.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.Property(b => b.Reference).HasMaxLength(20).IsRequired();
        builder.HasIndex(b => b.Reference).IsUnique();

        // Status as string so the DB is readable and check constraints work.
        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(b => b.CancellationReason).HasMaxLength(500);
        builder.Property(b => b.RowVersion).IsRowVersion();

        // Filtered unique index. DB rejects the second active booking, not the app.
        builder.HasIndex(b => b.TimeSlotId)
            .IsUnique()
            .HasFilter("[Status] IN ('Requested','Confirmed')")
            .HasDatabaseName("UX_Bookings_ActiveSlot");

        builder.HasIndex(b => new { b.ClientUserId, b.Status });
        builder.HasIndex(b => new { b.StaffUserId, b.Status });
        builder.HasIndex(b => new { b.Status, b.CreatedAtUtc });

        // Restrict deletes. We never want a slot deletion to nuke bookings.
        builder.HasOne(b => b.Service)
            .WithMany()
            .HasForeignKey(b => b.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.TimeSlot)
            .WithMany()
            .HasForeignKey(b => b.TimeSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Booking_Status",
                "[Status] IN ('Requested','Confirmed','Completed','Cancelled')");
        });
    }
}
