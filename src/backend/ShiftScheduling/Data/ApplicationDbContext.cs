using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<SchedulePeriod> SchedulePeriods => Set<SchedulePeriod>();
    public DbSet<ShiftType> ShiftTypes => Set<ShiftType>();
    public DbSet<StaffingRequirement> StaffingRequirements => Set<StaffingRequirement>();
    public DbSet<Availability> Availabilities => Set<Availability>();
    public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<ShiftTransferRequest> ShiftTransferRequests => Set<ShiftTransferRequest>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUserAccounts(modelBuilder);
        ConfigureEmployees(modelBuilder);
        ConfigureSchedulePeriods(modelBuilder);
        ConfigureShiftTypes(modelBuilder);
        ConfigureStaffingRequirements(modelBuilder);
        ConfigureAvailabilities(modelBuilder);
        ConfigureWorkSchedules(modelBuilder);
        ConfigureShiftAssignments(modelBuilder);
        ConfigureLeaveRequests(modelBuilder);
        ConfigureShiftTransferRequests(modelBuilder);
        ConfigureNotifications(modelBuilder);
        SeedWeekOneData(modelBuilder);
    }

    private static void ConfigureUserAccounts(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<UserAccount>();

        entity.ToTable("UserAccounts");
        entity.HasKey(x => x.UserAccountId);

        entity.Property(x => x.Username)
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(x => x.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

        entity.HasIndex(x => x.Username)
            .IsUnique();

        entity.HasOne(x => x.CreatedByAccount)
            .WithMany(x => x.CreatedAccounts)
            .HasForeignKey(x => x.CreatedByAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureEmployees(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Employee>();

        entity.ToTable("Employees");
        entity.HasKey(x => x.EmployeeId);

        entity.Property(x => x.EmployeeCode)
            .HasMaxLength(30)
            .IsRequired();

        entity.Property(x => x.FullName)
            .HasMaxLength(150)
            .IsRequired();

        entity.Property(x => x.PhoneNumber)
            .HasMaxLength(20);

        entity.Property(x => x.Email)
            .HasMaxLength(150);

        entity.HasIndex(x => x.EmployeeCode)
            .IsUnique();

        entity.HasIndex(x => x.UserAccountId)
            .IsUnique()
            .HasFilter("[UserAccountId] IS NOT NULL");

        entity.HasOne(x => x.UserAccount)
            .WithOne(x => x.Employee)
            .HasForeignKey<Employee>(x => x.UserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureSchedulePeriods(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SchedulePeriod>();

        entity.ToTable("SchedulePeriods");
        entity.HasKey(x => x.SchedulePeriodId);

        entity.Property(x => x.PeriodName)
            .HasMaxLength(150)
            .IsRequired();

        entity.HasIndex(x => x.WeekStartDate)
            .IsUnique();

        entity.HasOne(x => x.CreatedByAccount)
            .WithMany()
            .HasForeignKey(x => x.CreatedByAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureShiftTypes(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ShiftType>();

        entity.ToTable("ShiftTypes");
        entity.HasKey(x => x.ShiftTypeId);

        entity.Property(x => x.ShiftCode)
            .HasMaxLength(30)
            .IsRequired();

        entity.Property(x => x.ShiftName)
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(x => x.DurationHours)
            .HasPrecision(4, 2);

        entity.HasIndex(x => x.ShiftCode)
            .IsUnique();

        entity.HasIndex(x => x.DisplayOrder)
            .IsUnique();
    }

    private static void ConfigureStaffingRequirements(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<StaffingRequirement>();

        entity.ToTable("StaffingRequirements");
        entity.HasKey(x => x.StaffingRequirementId);

        entity.HasIndex(x => new
        {
            x.SchedulePeriodId,
            x.WorkDate,
            x.ShiftTypeId
        }).IsUnique();

        entity.HasOne(x => x.SchedulePeriod)
            .WithMany(x => x.StaffingRequirements)
            .HasForeignKey(x => x.SchedulePeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.ShiftType)
            .WithMany(x => x.StaffingRequirements)
            .HasForeignKey(x => x.ShiftTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureAvailabilities(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Availability>();

        entity.ToTable("Availabilities");
        entity.HasKey(x => x.AvailabilityId);

        entity.HasIndex(x => new
        {
            x.EmployeeId,
            x.SchedulePeriodId,
            x.WorkDate,
            x.ShiftTypeId
        }).IsUnique();

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

    private static void ConfigureWorkSchedules(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<WorkSchedule>();

        entity.ToTable("WorkSchedules");
        entity.HasKey(x => x.WorkScheduleId);

        entity.Property(x => x.ScheduleName)
            .HasMaxLength(150)
            .IsRequired();

        entity.HasOne(x => x.SchedulePeriod)
            .WithMany(x => x.WorkSchedules)
            .HasForeignKey(x => x.SchedulePeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.CreatedByAccount)
            .WithMany(x => x.CreatedWorkSchedules)
            .HasForeignKey(x => x.CreatedByAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.PublishedByAccount)
            .WithMany(x => x.PublishedWorkSchedules)
            .HasForeignKey(x => x.PublishedByAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureShiftAssignments(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ShiftAssignment>();

        entity.ToTable("ShiftAssignments");
        entity.HasKey(x => x.ShiftAssignmentId);

        entity.HasIndex(x => new
        {
            x.WorkScheduleId,
            x.EmployeeId,
            x.WorkDate,
            x.ShiftTypeId
        }).IsUnique();

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

    private static void ConfigureLeaveRequests(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<LeaveRequest>();

        entity.ToTable("LeaveRequests");
        entity.HasKey(x => x.LeaveRequestId);

        entity.Property(x => x.Reason)
            .HasMaxLength(1000)
            .IsRequired();

        entity.Property(x => x.ManagerNote)
            .HasMaxLength(1000);

        entity.HasIndex(x => new
        {
            x.EmployeeId,
            x.SchedulePeriodId,
            x.LeaveDate,
            x.ShiftTypeId
        });

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

    private static void ConfigureShiftTransferRequests(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ShiftTransferRequest>();

        entity.ToTable("ShiftTransferRequests");
        entity.HasKey(x => x.ShiftTransferRequestId);

        entity.Property(x => x.Reason)
            .HasMaxLength(1000)
            .IsRequired();

        entity.Property(x => x.ManagerNote)
            .HasMaxLength(1000);

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

    private static void ConfigureNotifications(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Notification>();

        entity.ToTable("Notifications");
        entity.HasKey(x => x.NotificationId);

        entity.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        entity.Property(x => x.Content)
            .HasMaxLength(2000)
            .IsRequired();

        entity.HasIndex(x => new { x.ReceiverAccountId, x.IsRead, x.CreatedAt });

        entity.HasOne(x => x.ReceiverAccount)
            .WithMany(x => x.Notifications)
            .HasForeignKey(x => x.ReceiverAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void SeedWeekOneData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>().HasData(
            new UserAccount
            {
                UserAccountId = 1,
                Username = "admin",
                PasswordHash = "100000.ABEiM0RVZneImaq7zN3u/w==.voGHdh8BUEr7qGOgntpkfAlsfKyMx/CzNR/DLboeAU4=",
                Role = UserRole.Admin,
                IsLocked = false,
                CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new UserAccount
            {
                UserAccountId = 2,
                Username = "quanly",
                PasswordHash = "100000./+7dzLuqmYh3ZlVEMyIRAA==.UWV+wqzCmlDi0tlbWmy/DpHdbaaFtpczTXzp5MErAog=",
                Role = UserRole.Manager,
                IsLocked = false,
                CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedByAccountId = 1
            });

        modelBuilder.Entity<ShiftType>().HasData(
            new ShiftType
            {
                ShiftTypeId = 1,
                ShiftCode = "MORNING",
                ShiftName = "Ca sáng",
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(12, 0),
                DurationHours = 4,
                DisplayOrder = 1,
                IsActive = true
            },
            new ShiftType
            {
                ShiftTypeId = 2,
                ShiftCode = "AFTERNOON",
                ShiftName = "Ca chiều",
                StartTime = new TimeOnly(12, 0),
                EndTime = new TimeOnly(16, 0),
                DurationHours = 4,
                DisplayOrder = 2,
                IsActive = true
            },
            new ShiftType
            {
                ShiftTypeId = 3,
                ShiftCode = "EVENING",
                ShiftName = "Ca tối",
                StartTime = new TimeOnly(16, 0),
                EndTime = new TimeOnly(20, 0),
                DurationHours = 4,
                DisplayOrder = 3,
                IsActive = true
            });
    }
}
