using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Infrastructure.Persistence.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> entity)
    {
        entity.ToTable("Employees");
        entity.HasKey(x => x.EmployeeId);
        entity.Property(x => x.EmployeeCode).HasMaxLength(30).IsRequired();
        entity.Property(x => x.FullName).HasMaxLength(150).IsRequired().UseCollation("Vietnamese_100_CI_AI");
        entity.Property(x => x.PhoneNumber).HasMaxLength(20);
        entity.Property(x => x.Email).HasMaxLength(150);
        entity.HasIndex(x => x.EmployeeCode).IsUnique();
        entity.HasIndex(x => x.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
        entity.HasIndex(x => x.UserAccountId).IsUnique().HasFilter("[UserAccountId] IS NOT NULL");

        entity.HasOne(x => x.UserAccount)
            .WithOne(x => x.Employee)
            .HasForeignKey<Employee>(x => x.UserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
