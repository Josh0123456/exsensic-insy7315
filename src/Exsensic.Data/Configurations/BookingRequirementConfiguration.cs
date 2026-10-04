using Exsensic.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exsensic.Data.Configurations;


public class BookingRequirementConfiguration : IEntityTypeConfiguration<BookingRequirement>
{

    public void Configure(EntityTypeBuilder<BookingRequirement> builder)
    {
        builder.Property(r => r.FieldKey)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.FieldValue)
            .HasMaxLength(2000)
            .IsRequired();

        builder.HasIndex(r => new { r.BookingId, r.FieldKey })
            .IsUnique()
            .HasDatabaseName("UX_BookingRequirements_Booking_FieldKey");

        builder.HasOne<Booking>()
            .WithMany(b => b.Requirements)
            .HasForeignKey(r => r.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
