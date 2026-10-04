using System;
using System.Collections.Generic;
using System.Text;
using Exsensic.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exsensic.Data.Configurations;

public class TimeSlotConfiguration : IEntityTypeConfiguration<TimeSlot>
{
    public void Configure(EntityTypeBuilder<TimeSlot> builder)
    {
        builder.HasIndex(t => new { t.SlotDate, t.StartTime }).IsUnique();
        builder.Property(t => t.RowVersion).IsRowVersion();
        builder.Property(t => t.BlockReason).HasMaxLength(500);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_TimeSlot_EndAfterStart", "[EndTime] > [StartTime]");
        });
    }
}
