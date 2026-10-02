using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Infrastructure.Persistence.Configurations;

// Ánh xạ EF Core cho nhật ký thao tác, gồm khóa, quan hệ và giới hạn dữ liệu.
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> entity)
    {
        entity.ToTable("AuditLogs");
        entity.HasKey(x => x.AuditLogId);
        entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
        entity.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
        entity.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
        entity.Property(x => x.PreviousValues).HasColumnType("nvarchar(max)");
        entity.Property(x => x.NewValues).HasColumnType("nvarchar(max)");
        entity.HasIndex(x => new { x.ActorAccountId, x.CreatedAtUtc });
        entity.HasIndex(x => new { x.EntityName, x.EntityId, x.CreatedAtUtc });

        entity.HasOne(x => x.ActorAccount)
            .WithMany(x => x.AuditLogs)
            .HasForeignKey(x => x.ActorAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
