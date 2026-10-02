using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Infrastructure.Persistence.Configurations;

// Ánh xạ EF Core cho kỳ lịch 7 ngày và thời hạn đăng ký.
public sealed class SchedulePeriodConfiguration : IEntityTypeConfiguration<SchedulePeriod>
{
    public void Configure(EntityTypeBuilder<SchedulePeriod> entity)
    {
        entity.ToTable("SchedulePeriods");
        entity.HasKey(x => x.SchedulePeriodId);
        entity.Property(x => x.PeriodName).HasMaxLength(150).IsRequired();
        entity.HasIndex(x => x.WeekStartDate).IsUnique();

        entity.HasOne(x => x.CreatedByAccount)
            .WithMany()
            .HasForeignKey(x => x.CreatedByAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
