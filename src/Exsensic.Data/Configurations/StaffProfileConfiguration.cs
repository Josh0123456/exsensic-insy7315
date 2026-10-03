using Exsensic.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exsensic.Data.Configurations;

public class StaffProfileConfiguration : IEntityTypeConfiguration<StaffProfile>
{
    public void Configure(EntityTypeBuilder<StaffProfile> builder)
    {
        // UserId is the PK and the FK back to ApplicationUser.
        builder.HasKey(p => p.UserId);

        builder.Property(p => p.JobTitle).HasMaxLength(100).IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<StaffProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
