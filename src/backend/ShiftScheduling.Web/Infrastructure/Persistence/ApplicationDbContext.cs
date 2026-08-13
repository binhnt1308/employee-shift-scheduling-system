using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Infrastructure.Persistence.Seed;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext
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
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        ModelSeed.Apply(modelBuilder);
    }
}
