using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Infrastructure.Persistence.Configurations;

public sealed class ShiftAssignmentConfiguration : IEntityTypeConfiguration<ShiftAssignment>
{
    public void Configure(EntityTypeBuilder<ShiftAssignment> entity)
    {
        entity.ToTable("ShiftAssignments");
        entity.HasKey(x => x.ShiftAssignmentId);
        entity.HasIndex(x => new { x.WorkScheduleId, x.EmployeeId, x.WorkDate, x.ShiftTypeId }).IsUnique();

        entity.HasOne(x => x.WorkSchedule)
            .WithMany(x => x.ShiftAssignments)
            .HasForeignKey(x => x.WorkScheduleId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.Employee)
            .WithMany(x => x.ShiftAssignments)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.ShiftType)
            .WithMany(x => x.ShiftAssignments)
            .HasForeignKey(x => x.ShiftTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
