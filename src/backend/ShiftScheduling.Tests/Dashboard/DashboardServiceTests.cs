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

        var service = new DashboardService(accountRepo, employeeRepo, shiftTypeRepo, scheduleRepo);

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

        var service = new DashboardService(accountRepo, employeeRepo, shiftTypeRepo, scheduleRepo);

        var result = await service.GetDashboardAsync(userAccountId: 6);

        Assert.NotNull(result);
        Assert.True(result.HasSchedule);
        Assert.False(result.HasMySchedule);
    }
}
