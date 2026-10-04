using Exsensic.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exsensic.Data.Configurations;

public class BookingStatusHistoryConfiguration : IEntityTypeConfiguration<BookingStatusHistory>
{

    public void Configure(EntityTypeBuilder<BookingStatusHistory> builder)
    {

        builder.Property(h => h.FromStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(h => h.ToStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(h => h.Note)
            .HasMaxLength(500);


        builder.HasIndex(h => new { h.BookingId, h.ChangedAtUtc })
            .HasDatabaseName("IX_BookingStatusHistory_Booking_ChangedAt");

        builder.HasOne<Booking>()
            .WithMany(b => b.StatusHistory)
            .HasForeignKey(h => h.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}
