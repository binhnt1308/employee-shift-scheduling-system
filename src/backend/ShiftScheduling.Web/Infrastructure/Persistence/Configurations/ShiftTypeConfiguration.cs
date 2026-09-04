using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Infrastructure.Persistence.Configurations;

public sealed class ShiftTypeConfiguration : IEntityTypeConfiguration<ShiftType>
{
    public void Configure(EntityTypeBuilder<ShiftType> entity)
    {
        entity.ToTable("ShiftTypes");
        entity.HasKey(x => x.ShiftTypeId);
        entity.Property(x => x.ShiftCode).HasMaxLength(30).IsRequired();
        entity.Property(x => x.ShiftName).HasMaxLength(100).IsRequired().UseCollation("Vietnamese_100_CI_AI");
        entity.Property(x => x.DurationHours).HasPrecision(4, 2);
        entity.Property(x => x.ConfigurationVersion).HasDefaultValue(1);
        entity.Property(x => x.MaxShiftsPerEmployeePerDay).HasDefaultValue(1);
        entity.Property(x => x.AllowSplitShift).HasDefaultValue(false);
        entity.ToTable("ShiftTypes", t => t.HasCheckConstraint(
            "CK_ShiftTypes_MaxShiftsPerEmployeePerDay",
            "[MaxShiftsPerEmployeePerDay] IN (1, 2)"));
        entity.HasIndex(x => new { x.ShiftCode, x.ConfigurationVersion }).IsUnique();
        entity.HasIndex(x => new { x.DisplayOrder, x.ConfigurationVersion }).IsUnique();
    }
}
