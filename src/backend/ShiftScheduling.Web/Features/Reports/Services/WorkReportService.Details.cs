using ShiftScheduling.Features.Reports.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Reports.Services;

public sealed partial class WorkReportService
{
    private List<WorkdayDetailItemViewModel> BuildWorkdayDetails(
        List<ShiftAssignment> assignments,
        int schedulePeriodId,
        ApprovedLeaveLookup leaveLookup,
        int? employeeIdFilter,
        EmployeePosition? positionFilter,
        EmploymentType? employmentTypeFilter,
        bool isPublished,
        bool isClosed)
    {
        var vietnamNow = _clock.ToVietnamTime(_clock.UtcNow);
        var filteredAssignments = FilterAssignments(
            assignments, employeeIdFilter, positionFilter, employmentTypeFilter);

        var details = new List<WorkdayDetailItemViewModel>();
        foreach (var a in filteredAssignments
            .OrderBy(a => a.WorkDate)
            .ThenBy(a => a.ShiftType?.StartTime ?? TimeOnly.MinValue)
            .ThenBy(a => a.Employee?.EmployeeCode ?? string.Empty))
        {
            details.Add(CreateWorkdayDetailItem(
                a, schedulePeriodId, leaveLookup, vietnamNow, isPublished, isClosed));
        }

        return details;
    }

    private static IEnumerable<ShiftAssignment> FilterAssignments(
        List<ShiftAssignment> assignments,
        int? employeeIdFilter,
        EmployeePosition? positionFilter,
        EmploymentType? employmentTypeFilter)
    {
        var query = assignments.AsEnumerable();
        if (employeeIdFilter.HasValue && employeeIdFilter.Value > 0)
        {
            query = query.Where(a => a.EmployeeId == employeeIdFilter.Value);
        }
        if (positionFilter.HasValue)
        {
            query = query.Where(a => a.Employee?.Position == positionFilter.Value);
        }
        if (employmentTypeFilter.HasValue)
        {
            query = query.Where(a => a.Employee?.EmploymentType == employmentTypeFilter.Value);
        }
        return query;
    }

    private static WorkdayDetailItemViewModel CreateWorkdayDetailItem(
        ShiftAssignment a,
        int schedulePeriodId,
        ApprovedLeaveLookup leaveLookup,
        DateTime vietnamNow,
        bool isPublished,
        bool isClosed)
    {
        var (systemHours, effectiveHours, hasApprovedLeave) =
            CalculateAssignmentHours(a, schedulePeriodId, leaveLookup);
        decimal plannedHours = a.ShiftType?.DurationHours ?? 0m;
        bool hasEnded = false;
        if (a.ShiftType != null)
        {
            var shiftEndDate = a.WorkDate;
            if (a.ShiftType.EndTime <= a.ShiftType.StartTime)
            {
                shiftEndDate = shiftEndDate.AddDays(1);
            }
            var shiftEndDateTime = shiftEndDate.ToDateTime(a.ShiftType.EndTime);
            hasEnded = vietnamNow >= shiftEndDateTime;
        }

        return new WorkdayDetailItemViewModel
        {
            ShiftAssignmentId = a.ShiftAssignmentId,
            EmployeeId = a.EmployeeId,
            EmployeeCode = a.Employee?.EmployeeCode ?? string.Empty,
            FullName = a.Employee?.FullName ?? string.Empty,
            WorkDate = a.WorkDate,
            ShiftTypeId = a.ShiftTypeId,
            ShiftName = a.ShiftType?.ShiftName ?? string.Empty,
            ShiftTimeRange = a.ShiftType != null
                ? $"{a.ShiftType.StartTime:HH\\:mm} - {a.ShiftType.EndTime:HH\\:mm}"
                : string.Empty,
            PlannedHours = plannedHours,
            ActualWorkedHours = a.ActualWorkedHours,
            SystemCalculatedHours = systemHours,
            EffectiveWorkedHours = effectiveHours,
            WorkdayAdjustmentReason = a.WorkdayAdjustmentReason,
            HasApprovedLeave = hasApprovedLeave,
            CanAdjust = isPublished && hasEnded,
            IsScheduleClosed = isClosed
        };
    }

    private static (decimal SystemCalculatedHours, decimal EffectiveWorkedHours, bool HasApprovedLeave) CalculateAssignmentHours(
        ShiftAssignment assignment,
        int schedulePeriodId,
        ApprovedLeaveLookup leaveLookup)
    {
        bool hasApprovedLeave = leaveLookup.HasApprovedLeave(
            assignment.EmployeeId, schedulePeriodId, assignment.WorkDate, assignment.ShiftTypeId);
        decimal plannedHours = assignment.ShiftType?.DurationHours ?? 0m;
        decimal systemCalculatedHours = hasApprovedLeave ? 0m : plannedHours;
        decimal effectiveWorkedHours = assignment.ActualWorkedHours ?? systemCalculatedHours;
        return (systemCalculatedHours, effectiveWorkedHours, hasApprovedLeave);
    }

    private sealed class ApprovedLeaveLookup
    {
        private readonly HashSet<(int EmployeeId, int SchedulePeriodId, DateOnly LeaveDate, int ShiftTypeId)> _shiftLeaves = new();
        private readonly HashSet<(int EmployeeId, int SchedulePeriodId, DateOnly LeaveDate)> _fullDayLeaves = new();

        public ApprovedLeaveLookup(IEnumerable<LeaveRequest> approvedLeaves, int schedulePeriodId)
        {
            foreach (var leave in approvedLeaves)
            {
                if (leave.Status == LeaveRequestStatus.Approved && leave.SchedulePeriodId == schedulePeriodId)
                {
                    if (leave.ShiftTypeId.HasValue)
                    {
                        _shiftLeaves.Add((leave.EmployeeId, leave.SchedulePeriodId, leave.LeaveDate, leave.ShiftTypeId.Value));
                    }
                    else
                    {
                        _fullDayLeaves.Add((leave.EmployeeId, leave.SchedulePeriodId, leave.LeaveDate));
                    }
                }
            }
        }

        public bool HasApprovedLeave(int employeeId, int schedulePeriodId, DateOnly workDate, int shiftTypeId)
        {
            return _fullDayLeaves.Contains((employeeId, schedulePeriodId, workDate))
                || _shiftLeaves.Contains((employeeId, schedulePeriodId, workDate, shiftTypeId));
        }
    }
}
