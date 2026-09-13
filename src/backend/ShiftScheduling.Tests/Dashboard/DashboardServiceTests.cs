using ShiftScheduling.Features.Dashboard.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Tests.Fakes;
using Xunit;

namespace ShiftScheduling.Tests.Dashboard;

public sealed class DashboardServiceTests
{
    [Fact(DisplayName = "TC-DASH-EMP-01 - Employee has shift assignment -> HasMySchedule is true")]
    public async Task GetDashboardAsync_WhenEmployeeHasAssignment_ReturnsHasMyScheduleTrue()
    {
        var accountRepo = new FakeAccountRepository();
        var employeeRepo = new FakeEmployeeRepository();
        var shiftTypeRepo = new FakeShiftTypeRepository();
        var scheduleRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();

        var emp = new Employee
        {
            EmployeeId = 10,
            EmployeeCode = "NV10",
            FullName = "Nguyễn Văn X"
        };
        var userAcc = new UserAccount
        {
            UserAccountId = 5,
            Username = "nv10",
            Role = UserRole.Employee,
            Employee = emp
        };
        accountRepo.Accounts.Add(userAcc);

        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 1,
            SchedulePeriodId = 100,
            Status = ScheduleStatus.Published,
            ShiftAssignments = new List<ShiftAssignment>
            {
                new() { ShiftAssignmentId = 101, EmployeeId = 10, WorkDate = new DateOnly(2026, 8, 20), ShiftTypeId = 1 }
            }
        };
        scheduleRepo.Schedules.Add(publishedSchedule);

        var period = new SchedulePeriod
        {
            SchedulePeriodId = 100,
            PeriodName = "Kỳ 100",
            WeekStartDate = new DateOnly(2026, 8, 17),
            WeekEndDate = new DateOnly(2026, 8, 23)
        };
        period.WorkSchedules.Add(publishedSchedule);
        periodRepo.Add(period);

        var clock = new FakeClock(new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc));

        var service = new DashboardService(accountRepo, employeeRepo, shiftTypeRepo, periodRepo, clock);

        var result = await service.GetDashboardAsync(userAccountId: 5);

        Assert.NotNull(result);
        Assert.True(result.HasSchedule);
        Assert.True(result.HasMySchedule);
    }

    [Fact(DisplayName = "TC-DASH-EMP-02 - Employee has no assignment -> HasMySchedule is false")]
    public async Task GetDashboardAsync_WhenEmployeeHasNoAssignment_ReturnsHasMyScheduleFalse()
    {
        var accountRepo = new FakeAccountRepository();
        var employeeRepo = new FakeEmployeeRepository();
        var shiftTypeRepo = new FakeShiftTypeRepository();
        var scheduleRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();

        var emp = new Employee
        {
            EmployeeId = 20,
            EmployeeCode = "NV20",
            FullName = "Trần Văn Y"
        };
        var userAcc = new UserAccount
        {
            UserAccountId = 6,
            Username = "nv20",
            Role = UserRole.Employee,
            Employee = emp
        };
        accountRepo.Accounts.Add(userAcc);

        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 1,
            SchedulePeriodId = 100,
            Status = ScheduleStatus.Published,
            ShiftAssignments = new List<ShiftAssignment>
            {
                new() { ShiftAssignmentId = 101, EmployeeId = 10, WorkDate = new DateOnly(2026, 8, 20), ShiftTypeId = 1 }
            }
        };
        scheduleRepo.Schedules.Add(publishedSchedule);

        var period = new SchedulePeriod
        {
            SchedulePeriodId = 100,
            PeriodName = "Kỳ 100",
            WeekStartDate = new DateOnly(2026, 8, 17),
            WeekEndDate = new DateOnly(2026, 8, 23)
        };
        period.WorkSchedules.Add(publishedSchedule);
        periodRepo.Add(period);

        var clock = new FakeClock(new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc));

        var service = new DashboardService(accountRepo, employeeRepo, shiftTypeRepo, periodRepo, clock);

        var result = await service.GetDashboardAsync(userAccountId: 6);

        Assert.NotNull(result);
        Assert.True(result.HasSchedule);
        Assert.False(result.HasMySchedule);
    }

    [Fact(DisplayName = "TC-DASH-ACTIVE-01 - Dashboard only counts and returns active shifts")]
    public async Task GetDashboardAsync_OnlyCountsAndReturnsActiveShifts()
    {
        var accountRepo = new FakeAccountRepository();
        var employeeRepo = new FakeEmployeeRepository();
        var shiftTypeRepo = new FakeShiftTypeRepository();
        var periodRepo = new FakeSchedulePeriodRepository();

        // 3 active shifts and 3 inactive historical shifts
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng (v1)", IsActive = false, ConfigurationVersion = 1 });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều (v1)", IsActive = false, ConfigurationVersion = 1 });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối (v1)", IsActive = false, ConfigurationVersion = 1 });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 4, ShiftCode = "MORNING", ShiftName = "Ca sáng (v2)", IsActive = true, ConfigurationVersion = 2, StartTime = new(8, 0), EndTime = new(12, 0) });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 5, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều (v2)", IsActive = true, ConfigurationVersion = 2, StartTime = new(12, 0), EndTime = new(16, 0) });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 6, ShiftCode = "EVENING", ShiftName = "Ca tối (v2)", IsActive = true, ConfigurationVersion = 2, StartTime = new(16, 0), EndTime = new(20, 0) });

        var clock = new FakeClock(new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc));
        var service = new DashboardService(accountRepo, employeeRepo, shiftTypeRepo, periodRepo, clock);

        var result = await service.GetDashboardAsync();

        Assert.NotNull(result);
        Assert.Equal(3, result.ShiftTypeCount);
        Assert.Equal(3, result.ShiftTypes.Count);
        Assert.All(result.ShiftTypes, s => Assert.True(s.IsActive));
        Assert.All(result.ShiftTypes, s => Assert.Equal(2, s.ConfigurationVersion));
    }

    [Fact(DisplayName = "TC-DASH-MGR-01 - Manager receives NeedsAttention for overdue unassigned period")]
    public async Task GetDashboardAsync_WhenPeriodOverdueUnassigned_ReturnsNeedsAttentionTrue()
    {
        var accountRepo = new FakeAccountRepository();
        var employeeRepo = new FakeEmployeeRepository();
        var shiftTypeRepo = new FakeShiftTypeRepository();
        var periodRepo = new FakeSchedulePeriodRepository();

        var period = new SchedulePeriod
        {
            SchedulePeriodId = 200,
            PeriodName = "Kỳ quá hạn",
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16),
            AvailabilityDeadline = new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc)
        };
        periodRepo.Add(period);

        // Current date is 2026-08-12 (during period, period has started, no schedule)
        var clock = new FakeClock(new DateTime(2026, 8, 12, 5, 0, 0, DateTimeKind.Utc));
        var service = new DashboardService(accountRepo, employeeRepo, shiftTypeRepo, periodRepo, clock);

        var result = await service.GetDashboardAsync();

        Assert.NotNull(result);
        Assert.Equal("Quá hạn – chưa phân công", result.ScheduleStatusDisplay);
        Assert.True(result.NeedsAttention);
        Assert.NotNull(result.AttentionMessage);
    }

    [Fact(DisplayName = "TC-DASH-EMP-03 - Employee neutral status: unpublished, published+assigned, published+not assigned")]
    public async Task GetDashboardAsync_ForEmployee_ReturnsNeutralStatusScenarios()
    {
        var accountRepo = new FakeAccountRepository();
        var employeeRepo = new FakeEmployeeRepository();
        var shiftTypeRepo = new FakeShiftTypeRepository();
        var periodRepo = new FakeSchedulePeriodRepository();

        var emp = new Employee { EmployeeId = 30, EmployeeCode = "NV30", FullName = "Lê Văn C" };
        var userAcc = new UserAccount { UserAccountId = 7, Username = "nv30", Role = UserRole.Employee, Employee = emp };
        accountRepo.Accounts.Add(userAcc);

        var period = new SchedulePeriod
        {
            SchedulePeriodId = 300,
            PeriodName = "Kỳ hiện tại",
            WeekStartDate = new DateOnly(2026, 8, 17),
            WeekEndDate = new DateOnly(2026, 8, 23)
        };
        periodRepo.Add(period);

        var clock = new FakeClock(new DateTime(2026, 8, 18, 5, 0, 0, DateTimeKind.Utc));
        var service = new DashboardService(accountRepo, employeeRepo, shiftTypeRepo, periodRepo, clock);

        // Scenario A: Schedule not published (no schedule yet)
        var resultA = await service.GetDashboardAsync(userAccountId: 7);
        Assert.Equal("Lịch làm việc chưa được công bố", resultA.EmployeeScheduleStatusDisplay);
        Assert.Equal("17/08/2026 - 23/08/2026", resultA.TargetPeriodDateRange);

        // Scenario B: Published schedule without assignment for employee 30
        var publishedScheduleNoAssign = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 300,
            Status = ScheduleStatus.Published,
            ShiftAssignments = new List<ShiftAssignment>()
        };
        period.WorkSchedules.Add(publishedScheduleNoAssign);

        var resultB = await service.GetDashboardAsync(userAccountId: 7);
        Assert.Equal("Chưa có ca được phân công", resultB.EmployeeScheduleStatusDisplay);

        // Scenario C: Published schedule with assignment for employee 30
        publishedScheduleNoAssign.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 501,
            EmployeeId = 30,
            WorkDate = new DateOnly(2026, 8, 18),
            ShiftTypeId = 1
        });

        var resultC = await service.GetDashboardAsync(userAccountId: 7);
        Assert.Equal("Đã có lịch làm việc", resultC.EmployeeScheduleStatusDisplay);
        Assert.Equal("Kỳ lịch hiện tại", resultC.EmployeePeriodLabel);
    }

    [Fact(DisplayName = "TC-DASH-EMP-04 - EmployeePeriodLabel: Current period, upcoming period, no period")]
    public async Task GetDashboardAsync_EmployeePeriodLabel_ComputesCorrectLabels()
    {
        var accountRepo = new FakeAccountRepository();
        var employeeRepo = new FakeEmployeeRepository();
        var shiftTypeRepo = new FakeShiftTypeRepository();
        var periodRepo = new FakeSchedulePeriodRepository();

        var emp = new Employee { EmployeeId = 31, EmployeeCode = "NV31", FullName = "Vũ Thị D" };
        var userAcc = new UserAccount { UserAccountId = 8, Username = "nv31", Role = UserRole.Employee, Employee = emp };
        accountRepo.Accounts.Add(userAcc);

        // Case 1: No period at all
        var clock1 = new FakeClock(new DateTime(2026, 8, 10, 5, 0, 0, DateTimeKind.Utc));
        var service1 = new DashboardService(accountRepo, employeeRepo, shiftTypeRepo, periodRepo, clock1);
        var resultNoPeriod = await service1.GetDashboardAsync(userAccountId: 8);
        Assert.Equal("Chưa có kỳ lịch", resultNoPeriod.EmployeePeriodLabel);
        Assert.Equal("Chưa có kỳ lịch", resultNoPeriod.EmployeeScheduleStatusDisplay);

        // Case 2: Today is within WeekStartDate..WeekEndDate (Current period)
        var currentPeriod = new SchedulePeriod
        {
            SchedulePeriodId = 301,
            PeriodName = "Tên kỳ nội bộ quản lý 1",
            WeekStartDate = new DateOnly(2026, 8, 17),
            WeekEndDate = new DateOnly(2026, 8, 23)
        };
        periodRepo.Add(currentPeriod);

        var clock2 = new FakeClock(new DateTime(2026, 8, 18, 5, 0, 0, DateTimeKind.Utc));
        var service2 = new DashboardService(accountRepo, employeeRepo, shiftTypeRepo, periodRepo, clock2);
        var resultCurrent = await service2.GetDashboardAsync(userAccountId: 8);
        Assert.Equal("Kỳ lịch hiện tại", resultCurrent.EmployeePeriodLabel);
        // Note: TargetPeriodName is still set for managers, but employee label is strictly computed
        Assert.Equal("Tên kỳ nội bộ quản lý 1", resultCurrent.TargetPeriodName);

        // Case 3: Today is before WeekStartDate (Upcoming period)
        var periodRepoUpcoming = new FakeSchedulePeriodRepository();
        var upcomingPeriod = new SchedulePeriod
        {
            SchedulePeriodId = 302,
            PeriodName = "Tên kỳ nội bộ quản lý 2",
            WeekStartDate = new DateOnly(2026, 8, 24),
            WeekEndDate = new DateOnly(2026, 8, 30)
        };
        periodRepoUpcoming.Add(upcomingPeriod);

        var clock3 = new FakeClock(new DateTime(2026, 8, 20, 5, 0, 0, DateTimeKind.Utc));
        var service3 = new DashboardService(accountRepo, employeeRepo, shiftTypeRepo, periodRepoUpcoming, clock3);
        var resultUpcoming = await service3.GetDashboardAsync(userAccountId: 8);
        Assert.Equal("Kỳ lịch sắp tới", resultUpcoming.EmployeePeriodLabel);
    }

    [Fact(DisplayName = "TC-DASH-EMP-05 - Employee Dashboard populates FullName, week schedule 7 days, and shift details")]
    public async Task GetDashboardAsync_WhenEmployeeLoggedIn_PopulatesFullNameAndWeekSchedule()
    {
        var accountRepo = new FakeAccountRepository();
        var employeeRepo = new FakeEmployeeRepository();
        var shiftTypeRepo = new FakeShiftTypeRepository();
        var scheduleRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();

        shiftTypeRepo.ShiftTypes.Add(new ShiftType
        {
            ShiftTypeId = 1,
            ShiftCode = "MORNING",
            ShiftName = "Ca sáng",
            IsActive = true,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0)
        });

        var emp = new Employee
        {
            EmployeeId = 50,
            EmployeeCode = "NV50",
            FullName = "Nguyễn Văn Test"
        };
        var userAcc = new UserAccount
        {
            UserAccountId = 50,
            Username = "nv50",
            Role = UserRole.Employee,
            Employee = emp
        };
        accountRepo.Accounts.Add(userAcc);

        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 500,
            SchedulePeriodId = 500,
            Status = ScheduleStatus.Published,
            ShiftAssignments = new List<ShiftAssignment>
            {
                new() { ShiftAssignmentId = 5001, EmployeeId = 50, WorkDate = new DateOnly(2026, 9, 8), ShiftTypeId = 1 },
                new() { ShiftAssignmentId = 5002, EmployeeId = 50, WorkDate = new DateOnly(2026, 9, 10), ShiftTypeId = 1 }
            }
        };
        scheduleRepo.Schedules.Add(publishedSchedule);

        var period = new SchedulePeriod
        {
            SchedulePeriodId = 500,
            PeriodName = "Kỳ 500",
            WeekStartDate = new DateOnly(2026, 9, 7),
            WeekEndDate = new DateOnly(2026, 9, 13)
        };
        period.WorkSchedules.Add(publishedSchedule);
        periodRepo.Add(period);

        var clock = new FakeClock(new DateTime(2026, 9, 8, 8, 30, 0, DateTimeKind.Utc));
        var service = new DashboardService(accountRepo, employeeRepo, shiftTypeRepo, periodRepo, clock);

        var result = await service.GetDashboardAsync(userAccountId: 50);

        Assert.NotNull(result);
        Assert.Equal("Nguyễn Văn Test", result.EmployeeFullName);
        Assert.True(result.HasSchedule);
        Assert.True(result.HasMySchedule);
        Assert.Equal("badge-active", result.EmployeeScheduleBadgeClass);
        Assert.Equal(2, result.EmployeeTotalAssignedShifts);
        Assert.Equal(7, result.EmployeeWeekSchedule.Count);

        // Monday (07/09) - no shift
        Assert.Empty(result.EmployeeWeekSchedule[0].Shifts);
        Assert.False(result.EmployeeWeekSchedule[0].IsToday);

        // Tuesday (08/09) - today, 1 shift
        Assert.True(result.EmployeeWeekSchedule[1].IsToday);
        Assert.Single(result.EmployeeWeekSchedule[1].Shifts);
        Assert.Equal("Ca sáng", result.EmployeeWeekSchedule[1].Shifts[0].ShiftName);
        Assert.Equal("08:00 - 12:00", result.EmployeeWeekSchedule[1].Shifts[0].ShiftHours);

        // Thursday (10/09) - 1 shift
        Assert.Single(result.EmployeeWeekSchedule[3].Shifts);
    }

    [Fact(DisplayName = "TC-DASH-EMP-06 - When registration open and schedule unpublished -> IsRegistrationOpen is true and badge is primary")]
    public async Task GetDashboardAsync_WhenRegistrationOpen_SetsIsRegistrationOpenTrue()
    {
        var accountRepo = new FakeAccountRepository();
        var employeeRepo = new FakeEmployeeRepository();
        var shiftTypeRepo = new FakeShiftTypeRepository();
        var periodRepo = new FakeSchedulePeriodRepository();

        var emp = new Employee { EmployeeId = 60, EmployeeCode = "NV60", FullName = "Trần Thị Test" };
        var userAcc = new UserAccount { UserAccountId = 60, Username = "nv60", Role = UserRole.Employee, Employee = emp };
        accountRepo.Accounts.Add(userAcc);

        var period = new SchedulePeriod
        {
            SchedulePeriodId = 600,
            PeriodName = "Kỳ mở đăng ký",
            WeekStartDate = new DateOnly(2026, 9, 14),
            WeekEndDate = new DateOnly(2026, 9, 20),
            RegistrationStatus = RegistrationStatus.Open,
            AvailabilityDeadline = new DateTime(2026, 9, 12, 17, 0, 0, DateTimeKind.Utc)
        };
        periodRepo.Add(period);

        // Current time is 2026-09-10 (before WeekStartDate and before AvailabilityDeadline)
        var clock = new FakeClock(new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc));
        var service = new DashboardService(accountRepo, employeeRepo, shiftTypeRepo, periodRepo, clock);

        var result = await service.GetDashboardAsync(userAccountId: 60);

        Assert.NotNull(result);
        Assert.True(result.IsRegistrationOpen);
        Assert.Equal("badge-primary-custom", result.EmployeeScheduleBadgeClass);
        Assert.Equal("Lịch làm việc chưa được công bố", result.EmployeeScheduleStatusDisplay);
    }
}
