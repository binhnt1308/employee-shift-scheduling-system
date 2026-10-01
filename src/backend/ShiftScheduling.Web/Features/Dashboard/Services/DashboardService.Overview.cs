using System.Globalization;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Dashboard.Services;

public sealed partial class DashboardService
{
    private sealed class DashboardOverviewData
    {
        public SchedulePeriod? TargetPeriod { get; init; }
        public WorkSchedule? WorkSchedule { get; init; }
        public string? TargetPeriodName { get; init; }
        public string? TargetPeriodDateRange { get; init; }
        public string ScheduleStatusDisplay { get; init; } = "Chưa có kỳ lịch";
        public bool NeedsAttention { get; init; }
        public string? AttentionMessage { get; init; }
        public bool HasSchedule { get; init; }
        public bool IsRegistrationOpen { get; init; }
    }

    private sealed class EmployeeScheduleStatusData
    {
        public string StatusDisplay { get; init; } = "Chưa có kỳ lịch";
        public string PeriodLabel { get; init; } = "Chưa có kỳ lịch";
        public string BadgeClass { get; init; } = "badge-ended";
    }

    private async Task<DashboardOverviewData> BuildOverviewAsync(
        DateTime currentUtc,
        DateOnly currentDate,
        CancellationToken cancellationToken)
    {
        var periods = await _schedulePeriodRepository
            .GetAllWithSchedulesAsync(cancellationToken);

        var targetPeriod = SelectTargetPeriod(periods, currentDate);
        if (targetPeriod is null)
        {
            return new DashboardOverviewData();
        }

        var workSchedule = targetPeriod.WorkSchedules
            .OrderByDescending(schedule => schedule.WorkScheduleId)
            .FirstOrDefault();

        var status = SchedulePeriodStatusHelper.CalculateEffectiveStatus(
            targetPeriod,
            workSchedule,
            currentUtc,
            currentDate);

        return new DashboardOverviewData
        {
            TargetPeriod = targetPeriod,
            WorkSchedule = workSchedule,
            TargetPeriodName = targetPeriod.PeriodName,
            TargetPeriodDateRange = FormatPeriodRange(targetPeriod),
            ScheduleStatusDisplay = status.StatusName,
            NeedsAttention = status.NeedsAttention,
            AttentionMessage = status.AttentionMessage,
            HasSchedule = workSchedule?.Status == ScheduleStatus.Published,
            IsRegistrationOpen = status.IsRegistrationOpen
        };
    }

    private static SchedulePeriod? SelectTargetPeriod(
        IEnumerable<SchedulePeriod> periods,
        DateOnly currentDate)
    {
        return periods.FirstOrDefault(
                period => period.WeekStartDate <= currentDate
                    && currentDate <= period.WeekEndDate)
            ?? periods
                .Where(period => period.WeekStartDate > currentDate)
                .OrderBy(period => period.WeekStartDate)
                .FirstOrDefault();
    }

    private static string FormatPeriodRange(SchedulePeriod period)
    {
        return $"{period.WeekStartDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}"
            + $" - {period.WeekEndDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";
    }

    private static EmployeeScheduleStatusData BuildEmployeeStatus(
        SchedulePeriod? targetPeriod,
        DateOnly currentDate,
        bool hasSchedule,
        bool hasMySchedule,
        bool isRegistrationOpen)
    {
        if (targetPeriod is null)
        {
            return new EmployeeScheduleStatusData();
        }

        var periodLabel = targetPeriod.WeekStartDate <= currentDate
            && currentDate <= targetPeriod.WeekEndDate
            ? "Kỳ lịch hiện tại"
            : "Kỳ lịch sắp tới";

        if (!hasSchedule)
        {
            return new EmployeeScheduleStatusData
            {
                StatusDisplay = "Lịch làm việc chưa được công bố",
                PeriodLabel = periodLabel,
                BadgeClass = isRegistrationOpen
                    ? "badge-primary-custom"
                    : "badge-info-custom"
            };
        }

        return hasMySchedule
            ? new EmployeeScheduleStatusData
            {
                StatusDisplay = "Đã có lịch làm việc",
                PeriodLabel = periodLabel,
                BadgeClass = "badge-active"
            }
            : new EmployeeScheduleStatusData
            {
                StatusDisplay = "Chưa có ca được phân công",
                PeriodLabel = periodLabel,
                BadgeClass = "badge-info-custom"
            };
    }

    private static DashboardViewModel CreateDashboardViewModel(
        int accountCount,
        int employeeCount,
        IReadOnlyList<ShiftType> shiftTypes,
        DashboardOverviewData overview,
        EmployeeDashboardData employeeDashboard,
        EmployeeScheduleStatusData employeeStatus)
    {
        return new DashboardViewModel
        {
            AccountCount = accountCount,
            EmployeeCount = employeeCount,
            ShiftTypeCount = shiftTypes.Count,
            ShiftTypes = shiftTypes,
            TargetPeriodName = overview.TargetPeriodName,
            TargetPeriodDateRange = overview.TargetPeriodDateRange,
            ScheduleStatusDisplay = overview.ScheduleStatusDisplay,
            NeedsAttention = overview.NeedsAttention,
            AttentionMessage = overview.AttentionMessage,
            HasSchedule = overview.HasSchedule,
            HasMySchedule = employeeDashboard.HasMySchedule,
            EmployeeScheduleStatusDisplay = employeeStatus.StatusDisplay,
            EmployeePeriodLabel = employeeStatus.PeriodLabel,
            EmployeeFullName = employeeDashboard.EmployeeFullName,
            IsRegistrationOpen = overview.IsRegistrationOpen,
            EmployeeScheduleBadgeClass = employeeStatus.BadgeClass,
            EmployeeWeekSchedule = employeeDashboard.EmployeeWeekSchedule,
            EmployeeTotalAssignedShifts =
                employeeDashboard.EmployeeTotalAssignedShifts
        };
    }
}
