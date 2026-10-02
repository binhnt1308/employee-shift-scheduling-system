using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Infrastructure.Persistence.Configurations;

// Ánh xạ EF Core cho yêu cầu chuyển phân công từ người gửi sang người nhận.
public sealed class ShiftTransferRequestConfiguration : IEntityTypeConfiguration<ShiftTransferRequest>
{
    public void Configure(EntityTypeBuilder<ShiftTransferRequest> entity)
    {
        entity.ToTable("ShiftTransferRequests");
        entity.HasKey(x => x.ShiftTransferRequestId);
        entity.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        entity.Property(x => x.ManagerNote).HasMaxLength(1000);
        entity.Property(x => x.RowVersion).IsRowVersion();

        entity.HasOne(x => x.ShiftAssignment)
            .WithMany(x => x.ShiftTransferRequests)
            .HasForeignKey(x => x.ShiftAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.SenderEmployee)
            .WithMany(x => x.SentShiftTransferRequests)
            .HasForeignKey(x => x.SenderEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.ReceiverEmployee)
            .WithMany(x => x.ReceivedShiftTransferRequests)
            .HasForeignKey(x => x.ReceiverEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.ApprovedByAccount)
            .WithMany(x => x.ReviewedShiftTransferRequests)
            .HasForeignKey(x => x.ApprovedByAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
