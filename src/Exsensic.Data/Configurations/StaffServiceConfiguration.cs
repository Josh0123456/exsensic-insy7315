using Exsensic.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exsensic.Data.Configurations;

public class StaffServiceConfiguration : IEntityTypeConfiguration<StaffService>
{
    public void Configure(EntityTypeBuilder<StaffService> builder)
    {
       
        builder.HasKey(ss => new { ss.StaffUserId, ss.ServiceId });

        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(ss => ss.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
