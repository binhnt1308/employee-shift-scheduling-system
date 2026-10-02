using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Infrastructure.Persistence.Configurations;

// Ánh xạ EF Core cho thông báo nội bộ gửi đến tài khoản người dùng.
public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> entity)
    {
        entity.ToTable("Notifications");
        entity.HasKey(x => x.NotificationId);
        entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Content).HasMaxLength(2000).IsRequired();
        entity.HasIndex(x => new { x.ReceiverAccountId, x.IsRead, x.CreatedAt });

        entity.HasOne(x => x.ReceiverAccount)
            .WithMany(x => x.Notifications)
            .HasForeignKey(x => x.ReceiverAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
