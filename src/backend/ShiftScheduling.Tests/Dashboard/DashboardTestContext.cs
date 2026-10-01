using ShiftScheduling.Features.Dashboard.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.Dashboard;

internal sealed class DashboardTestContext
{
    public FakeAccountRepository Accounts { get; } = new();
    public FakeEmployeeRepository Employees { get; } = new();
    public FakeShiftTypeRepository Shifts { get; } = new();
    public FakeSchedulePeriodRepository Periods { get; } = new();
    public FakeLeaveRequestRepository Leaves { get; } = new();

    public DashboardService CreateService(DateTime utcNow)
    {
        return new DashboardService(
            Accounts,
            Employees,
            Shifts,
            Periods,
            Leaves,
            new FakeClock(utcNow));
    }

    public Employee AddEmployeeAccount(
        int employeeId,
        int accountId,
        string employeeCode,
        string fullName)
    {
        var employee = new Employee
        {
            EmployeeId = employeeId,
            EmployeeCode = employeeCode,
            FullName = fullName
        };

        Accounts.Accounts.Add(new UserAccount
        {
            UserAccountId = accountId,
            Username = $"nv{employeeId}",
            Role = UserRole.Employee,
            Employee = employee
        });

        return employee;
    }

    public SchedulePeriod AddPeriod(
        int periodId,
        string periodName,
        DateOnly weekStart,
        DateOnly weekEnd)
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = periodId,
            PeriodName = periodName,
            WeekStartDate = weekStart,
            WeekEndDate = weekEnd
        };

        Periods.Add(period);
        return period;
    }

    public WorkSchedule AddPublishedSchedule(
        SchedulePeriod period,
        int scheduleId)
    {
        var schedule = new WorkSchedule
        {
            WorkScheduleId = scheduleId,
            SchedulePeriodId = period.SchedulePeriodId,
            Status = ScheduleStatus.Published,
            ShiftAssignments = new List<ShiftAssignment>()
        };

        period.WorkSchedules.Add(schedule);
        return schedule;
    }

    public ShiftType AddShift(
        int shiftTypeId,
        string shiftCode,
        string shiftName,
        bool isActive = true,
        int configurationVersion = 1,
        TimeOnly? startTime = null,
        TimeOnly? endTime = null)
    {
        var shift = new ShiftType
        {
            ShiftTypeId = shiftTypeId,
            ShiftCode = shiftCode,
            ShiftName = shiftName,
            IsActive = isActive,
            ConfigurationVersion = configurationVersion,
            StartTime = startTime ?? new TimeOnly(8, 0),
            EndTime = endTime ?? new TimeOnly(12, 0)
        };

        Shifts.ShiftTypes.Add(shift);
        return shift;
    }

    public ShiftAssignment AddAssignment(
        WorkSchedule schedule,
        int assignmentId,
        int employeeId,
        DateOnly workDate,
        int shiftTypeId)
    {
        var assignment = new ShiftAssignment
        {
            ShiftAssignmentId = assignmentId,
            EmployeeId = employeeId,
            WorkDate = workDate,
            ShiftTypeId = shiftTypeId,
            WorkSchedule = schedule
        };

        schedule.ShiftAssignments.Add(assignment);
        return assignment;
    }

    public void AddApprovedLeave(
        int leaveRequestId,
        int periodId,
        int employeeId,
        DateOnly leaveDate,
        int? shiftTypeId)
    {
        Leaves.Requests.Add(new LeaveRequest
        {
            LeaveRequestId = leaveRequestId,
            SchedulePeriodId = periodId,
            EmployeeId = employeeId,
            LeaveDate = leaveDate,
            ShiftTypeId = shiftTypeId,
            Status = LeaveRequestStatus.Approved
        });
    }
}
