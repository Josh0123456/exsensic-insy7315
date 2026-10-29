using Exsensic.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exsensic.Data.Configurations;

public class BookingRequirementConfiguration : IEntityTypeConfiguration<BookingRequirement>
{
    public void Configure(EntityTypeBuilder<BookingRequirement> builder)
    {
        // Keys come from the category templates (longest is 14 characters). A bounded length is
        // needed because SQL Server cannot index nvarchar(max).
        builder.Property(r => r.FieldKey).HasMaxLength(50).IsRequired();
        builder.Property(r => r.FieldValue).IsRequired();

        // One answer per field per booking, enforced by the DB rather than trusted to the app.
        builder.HasIndex(r => new { r.BookingId, r.FieldKey }).IsUnique();
    }
}
