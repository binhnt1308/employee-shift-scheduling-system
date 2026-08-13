using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Infrastructure.Persistence.Configurations;

public sealed class AvailabilityConfiguration : IEntityTypeConfiguration<Availability>
{
    public void Configure(EntityTypeBuilder<Availability> entity)
    {
        entity.ToTable("Availabilities");
        entity.HasKey(x => x.AvailabilityId);
        entity.HasIndex(x => new { x.EmployeeId, x.SchedulePeriodId, x.WorkDate, x.ShiftTypeId }).IsUnique();

        entity.HasOne(x => x.Employee)
            .WithMany(x => x.Availabilities)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.SchedulePeriod)
            .WithMany(x => x.Availabilities)
            .HasForeignKey(x => x.SchedulePeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.ShiftType)
            .WithMany(x => x.Availabilities)
            .HasForeignKey(x => x.ShiftTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
