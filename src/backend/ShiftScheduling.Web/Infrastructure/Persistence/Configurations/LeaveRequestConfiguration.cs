using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Infrastructure.Persistence.Configurations;

public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> entity)
    {
        entity.ToTable("LeaveRequests");
        entity.HasKey(x => x.LeaveRequestId);
        entity.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        entity.Property(x => x.ManagerNote).HasMaxLength(1000);
        entity.HasIndex(x => new { x.EmployeeId, x.SchedulePeriodId, x.LeaveDate, x.ShiftTypeId });

        entity.HasOne(x => x.Employee)
            .WithMany(x => x.LeaveRequests)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.SchedulePeriod)
            .WithMany(x => x.LeaveRequests)
            .HasForeignKey(x => x.SchedulePeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.ShiftType)
            .WithMany(x => x.LeaveRequests)
            .HasForeignKey(x => x.ShiftTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.ApprovedByAccount)
            .WithMany(x => x.ReviewedLeaveRequests)
            .HasForeignKey(x => x.ApprovedByAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
