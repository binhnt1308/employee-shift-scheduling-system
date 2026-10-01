using System.Globalization;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Dashboard.Services;

public sealed partial class DashboardService
{
    private sealed class EmployeeDashboardData
    {
        public string? EmployeeFullName { get; init; }
        public bool HasMySchedule { get; init; }
        public int EmployeeTotalAssignedShifts { get; init; }

        public IReadOnlyList<EmployeeDashboardDayScheduleViewModel>
            EmployeeWeekSchedule { get; init; } =
                Array.Empty<EmployeeDashboardDayScheduleViewModel>();
    }

    private async Task<EmployeeDashboardData> BuildEmployeeDashboardAsync(
        int? userAccountId,
        SchedulePeriod? targetPeriod,
        WorkSchedule? workSchedule,
        bool hasSchedule,
        IReadOnlyList<ShiftType> shiftTypes,
        DateOnly currentDate,
        CancellationToken cancellationToken)
    {
        if (!userAccountId.HasValue)
        {
            return new EmployeeDashboardData();
        }

        var account = await _accountRepository.GetByIdAsync(
            userAccountId.Value,
            includeEmployee: true,
            cancellationToken);

        if (account?.Employee is null)
        {
            return new EmployeeDashboardData();
        }

        var employee = account.Employee;
        var assignments = GetEmployeeAssignments(
            employee.EmployeeId,
            hasSchedule,
            workSchedule);

        var approvedLeaves = targetPeriod is null || !hasSchedule
            ? Array.Empty<LeaveRequest>()
            : await _leaveRequestRepository.GetApprovedLeavesByPeriodAsync(
                targetPeriod.SchedulePeriodId,
                cancellationToken);

        return new EmployeeDashboardData
        {
            EmployeeFullName = employee.FullName,
            HasMySchedule = assignments.Count > 0,
            EmployeeTotalAssignedShifts = assignments.Count(
                assignment => !HasApprovedLeave(assignment, approvedLeaves)),
            EmployeeWeekSchedule = BuildEmployeeWeekSchedule(
                targetPeriod,
                assignments,
                approvedLeaves,
                shiftTypes,
                currentDate)
        };
    }

    private static List<ShiftAssignment> GetEmployeeAssignments(
        int employeeId,
        bool hasSchedule,
        WorkSchedule? workSchedule)
    {
        if (!hasSchedule || workSchedule is null)
        {
            return new List<ShiftAssignment>();
        }

        return workSchedule.ShiftAssignments
            .Where(assignment => assignment.EmployeeId == employeeId)
            .ToList();
    }

    private static bool HasApprovedLeave(
        ShiftAssignment assignment,
        IReadOnlyList<LeaveRequest> approvedLeaves)
    {
        return approvedLeaves.Any(leave =>
            leave.EmployeeId == assignment.EmployeeId
            && leave.LeaveDate == assignment.WorkDate
            && (!leave.ShiftTypeId.HasValue
                || leave.ShiftTypeId.Value == assignment.ShiftTypeId));
    }

    private static List<EmployeeDashboardDayScheduleViewModel>
        BuildEmployeeWeekSchedule(
            SchedulePeriod? targetPeriod,
            IReadOnlyList<ShiftAssignment> assignments,
            IReadOnlyList<LeaveRequest> approvedLeaves,
            IReadOnlyList<ShiftType> shiftTypes,
            DateOnly currentDate)
    {
        if (targetPeriod is null)
        {
            return new List<EmployeeDashboardDayScheduleViewModel>();
        }

        var culture = new CultureInfo("vi-VN");
        var result = new List<EmployeeDashboardDayScheduleViewModel>();

        for (var dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            var date = targetPeriod.WeekStartDate.AddDays(dayOffset);

            result.Add(BuildEmployeeDashboardDay(
                date,
                currentDate,
                assignments,
                approvedLeaves,
                shiftTypes,
                culture));
        }

        return result;
    }

    private static EmployeeDashboardDayScheduleViewModel BuildEmployeeDashboardDay(
        DateOnly date,
        DateOnly currentDate,
        IReadOnlyList<ShiftAssignment> assignments,
        IReadOnlyList<LeaveRequest> approvedLeaves,
        IReadOnlyList<ShiftType> shiftTypes,
        CultureInfo culture)
    {
        var dayAssignments = assignments
            .Where(assignment => assignment.WorkDate == date)
            .ToList();

        return new EmployeeDashboardDayScheduleViewModel
        {
            Date = date,
            DayOfWeekName = culture.TextInfo.ToTitleCase(
                date.ToString("dddd", culture)),
            DateFormatted = date.ToString(
                "dd/MM",
                CultureInfo.InvariantCulture),
            IsToday = date == currentDate,
            Shifts = dayAssignments
                .Select(assignment => BuildEmployeeShiftItem(
                    assignment,
                    approvedLeaves,
                    shiftTypes))
                .ToList()
        };
    }

    private static EmployeeDashboardShiftItemViewModel BuildEmployeeShiftItem(
        ShiftAssignment assignment,
        IReadOnlyList<LeaveRequest> approvedLeaves,
        IReadOnlyList<ShiftType> shiftTypes)
    {
        var shiftType = shiftTypes.FirstOrDefault(
            shift => shift.ShiftTypeId == assignment.ShiftTypeId)
            ?? assignment.ShiftType;

        return new EmployeeDashboardShiftItemViewModel
        {
            ShiftName = shiftType?.ShiftName ?? "Ca làm",
            ShiftCode = shiftType?.ShiftCode ?? string.Empty,
            ShiftHours = shiftType is null
                ? string.Empty
                : $"{shiftType.StartTime:HH:mm} - {shiftType.EndTime:HH:mm}",
            HasApprovedLeave = HasApprovedLeave(
                assignment,
                approvedLeaves)
        };
    }
}
