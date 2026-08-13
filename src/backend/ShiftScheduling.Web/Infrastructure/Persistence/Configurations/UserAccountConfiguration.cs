using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Infrastructure.Persistence.Configurations;

public sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> entity)
    {
        entity.ToTable("UserAccounts");
        entity.HasKey(x => x.UserAccountId);
        entity.Property(x => x.Username).HasMaxLength(100).IsRequired();
        entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        entity.HasIndex(x => x.Username).IsUnique();

        entity.HasOne(x => x.CreatedByAccount)
            .WithMany(x => x.CreatedAccounts)
            .HasForeignKey(x => x.CreatedByAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
