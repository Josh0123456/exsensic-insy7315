using Exsensic.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exsensic.Data.Configurations;


public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{

    public void Configure(EntityTypeBuilder<Notification> builder)
    {
 
        builder.Property(n => n.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(n => n.Message)
            .HasMaxLength(1000)
            .IsRequired();

        builder.HasIndex(n => new { n.UserId, n.IsRead })
            .HasDatabaseName("IX_Notifications_User_IsRead");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(n => n.BookingId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
