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
        entity.Property(x => x.ShiftName).HasMaxLength(100).IsRequired();
        entity.Property(x => x.DurationHours).HasPrecision(4, 2);
        entity.HasIndex(x => x.ShiftCode).IsUnique();
        entity.HasIndex(x => x.DisplayOrder).IsUnique();
    }
}
