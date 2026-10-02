using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Infrastructure.Persistence.Configurations;

public sealed class AvailabilityConfiguration : IEntityTypeConfiguration<Availability>
{
    public void Configure(EntityTypeBuilder<Availability> entity)
    {
        entity.ToTable("Availabilities");
        entity.HasKey(x => x.AvailabilityId);
        entity.HasIndex(x => new { x.EmployeeId, x.SchedulePeriodId, x.WorkDate, x.ShiftTypeId }).IsUnique();

        // NC-02: Mức nguyện vọng, mặc định Normal=1 cho dữ liệu cũ (migration-safe).
        // DB-09: trạng thái dùng enum thay vì chuỗi tự do để tránh giá trị ngoài tập hợp cho phép.
        entity.Property(x => x.PreferenceLevel)
            .HasDefaultValue(AvailabilityPreferenceLevel.Normal)
            .HasConversion<int>();

        entity.ToTable(t => t.HasCheckConstraint(
            "CK_Availabilities_PreferenceLevel",
            "[PreferenceLevel] IN (1, 2)"));

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
