using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Infrastructure.Persistence.Configurations;

public sealed class WorkScheduleConfiguration : IEntityTypeConfiguration<WorkSchedule>
{
    public void Configure(EntityTypeBuilder<WorkSchedule> entity)
    {
        entity.ToTable("WorkSchedules");
        entity.HasKey(x => x.WorkScheduleId);
        entity.Property(x => x.ScheduleName).HasMaxLength(150).IsRequired();
        entity.Property(x => x.RowVersion).IsRowVersion();

        entity.HasOne(x => x.SchedulePeriod)
            .WithMany(x => x.WorkSchedules)
            .HasForeignKey(x => x.SchedulePeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.CreatedByAccount)
            .WithMany(x => x.CreatedWorkSchedules)
            .HasForeignKey(x => x.CreatedByAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.PublishedByAccount)
            .WithMany(x => x.PublishedWorkSchedules)
            .HasForeignKey(x => x.PublishedByAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
