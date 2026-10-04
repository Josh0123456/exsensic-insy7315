using System;
using System.Collections.Generic;
using System.Text;
using Exsensic.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exsensic.Data.Configurations;

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(120).IsRequired();
        builder.HasIndex(s => s.Name).IsUnique();
        builder.Property(s => s.Description).HasMaxLength(2000).IsRequired();
        builder.Property(s => s.BasePrice).HasColumnType("decimal(10,2)");
        builder.Property(s => s.RowVersion).IsRowVersion();

        builder.Property(s => s.Category).HasConversion<string>().HasMaxLength(20);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Service_Duration",
                "[DurationMinutes] BETWEEN 15 AND 480");
        });
    }
}
