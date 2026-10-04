using Exsensic.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exsensic.Data.Configurations;

public class ClientProfileConfiguration : IEntityTypeConfiguration<ClientProfile>
{
    public void Configure(EntityTypeBuilder<ClientProfile> builder)
    {
        // UserId is the PK and the FK back to ApplicationUser.
        builder.HasKey(p => p.UserId);

        builder.Property(p => p.CompanyName).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Phone).HasMaxLength(20).IsRequired();

        // Restrict so deleting a user doesn't silently orphan their profile.
        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<ClientProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
