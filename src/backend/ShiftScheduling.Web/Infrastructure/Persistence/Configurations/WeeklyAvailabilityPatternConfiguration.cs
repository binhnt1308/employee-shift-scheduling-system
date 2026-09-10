using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Infrastructure.Persistence.Configurations;

public sealed class WeeklyAvailabilityPatternConfiguration : IEntityTypeConfiguration<WeeklyAvailabilityPattern>
{
    public void Configure(EntityTypeBuilder<WeeklyAvailabilityPattern> entity)
    {
        entity.ToTable("WeeklyAvailabilityPatterns");
        entity.HasKey(x => x.WeeklyAvailabilityPatternId);

        // DB-05: cột định danh nghiệp vụ phải có UNIQUE (DB-13: chặn trùng bằng constraint).
        entity.HasIndex(x => new { x.EmployeeId, x.DayOfWeek, x.ShiftCode }).IsUnique();

        entity.Property(x => x.ShiftCode)
            .HasMaxLength(30)
            .IsRequired();

        // DB-09: PreferenceLevel dùng enum lưu dưới dạng int, không dùng chuỗi.
        entity.Property(x => x.PreferenceLevel)
            .HasDefaultValue(AvailabilityPreferenceLevel.Normal)
            .HasConversion<int>();

        // Check constraint: chỉ nhận ShiftCode cố định của hệ thống (BR03).
        entity.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_WeeklyAvailabilityPatterns_ShiftCode",
                "[ShiftCode] IN ('MORNING', 'AFTERNOON', 'EVENING')");

            t.HasCheckConstraint(
                "CK_WeeklyAvailabilityPatterns_DayOfWeek",
                "[DayOfWeek] BETWEEN 1 AND 7");

            t.HasCheckConstraint(
                "CK_WeeklyAvailabilityPatterns_PreferenceLevel",
                "[PreferenceLevel] IN (1, 2)");
        });

        entity.Property(x => x.CreatedAt).IsRequired();

        // DB-04: FK thật với ON DELETE Restrict – xóa Employee cần xử lý thủ công.
        entity.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
