using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Infrastructure.Persistence.Configurations;

// Ánh xạ EF Core cho định mức nhân sự theo ngày và ca trong kỳ lịch.
public sealed class StaffingRequirementConfiguration : IEntityTypeConfiguration<StaffingRequirement>
{
    public void Configure(EntityTypeBuilder<StaffingRequirement> entity)
    {
        entity.ToTable("StaffingRequirements");
        entity.HasKey(x => x.StaffingRequirementId);
        entity.HasIndex(x => new { x.SchedulePeriodId, x.WorkDate, x.ShiftTypeId }).IsUnique();

        entity.HasOne(x => x.SchedulePeriod)
            .WithMany(x => x.StaffingRequirements)
            .HasForeignKey(x => x.SchedulePeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.ShiftType)
            .WithMany(x => x.StaffingRequirements)
            .HasForeignKey(x => x.ShiftTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
