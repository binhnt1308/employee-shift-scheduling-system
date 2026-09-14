using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Features.Reports.ModelBinders;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Reports.ViewModels;

public sealed class WorkReportRowItemViewModel
{
    public int Index { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public EmployeePosition Position { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public int TotalShifts { get; set; }
    public int TotalWorkDays { get; set; }
    public decimal TotalHours { get; set; }
    public string FormattedTotalHours => TotalHours % 1 == 0
        ? $"{decimal.ToInt32(TotalHours)} h"
        : $"{TotalHours.ToString("0.##", new System.Globalization.CultureInfo("vi-VN"))} h";
    public string Warning { get; set; } = string.Empty;
    public bool IsTargetMet { get; set; }
}

public sealed class ShiftCoverageSummaryViewModel
{
    public int TotalRequiredSlots { get; set; }
    public int TotalCoveredSlots { get; set; }

    public int RequiredSalesSlots { get; set; }
    public int CoveredSalesSlots { get; set; }

    public int RequiredCashierSlots { get; set; }
    public int CoveredCashierSlots { get; set; }

    public decimal? CoverageRate { get; set; }

    public bool HasRequirements => TotalRequiredSlots > 0;

    public bool IsFullyCovered =>
        HasRequirements && TotalCoveredSlots == TotalRequiredSlots;

    public string FormattedCoverageRate =>
        CoverageRate.HasValue
            ? $"{CoverageRate.Value:0.#}%"
            : "Không áp dụng";
}

public sealed class ManagerWorkReportOverviewViewModel
{
    public int SchedulePeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public int? EmployeeIdFilter { get; set; }
    public EmployeePosition? PositionFilter { get; set; }
    public EmploymentType? EmploymentTypeFilter { get; set; }
    public List<ReportPeriodOptionViewModel> PeriodOptions { get; set; } = new();
    public List<ReportEmployeeOptionViewModel> EmployeeOptions { get; set; } = new();
    public List<WorkReportRowItemViewModel> Rows { get; set; } = new();
    public List<WorkdayDetailItemViewModel> Details { get; set; } = new();
    public ShiftCoverageSummaryViewModel? CoverageSummary { get; set; }
    public WorkloadFairnessSummaryViewModel? FairnessSummary { get; set; }
}

public sealed class WorkloadFairnessGroupViewModel
{
    public EmploymentType EmploymentType { get; set; }
    public int EmployeeCount { get; set; }
    public decimal MinimumScheduledHours { get; set; }
    public decimal MaximumScheduledHours { get; set; }
    public decimal SpreadHours { get; set; }
    public bool HasData => EmployeeCount > 0;
    public bool HasComparison => EmployeeCount >= 2;

    public string FormattedMinimumHours => FormatHours(MinimumScheduledHours);
    public string FormattedMaximumHours => FormatHours(MaximumScheduledHours);
    public string FormattedSpreadHours => FormatHours(SpreadHours);

    private static string FormatHours(decimal hours) => hours % 1 == 0
        ? $"{decimal.ToInt32(hours)} giờ"
        : $"{hours.ToString("0.##", new System.Globalization.CultureInfo("vi-VN"))} giờ";
}

public sealed class WorkloadFairnessSummaryViewModel
{
    public WorkloadFairnessGroupViewModel FullTime { get; set; } = new() { EmploymentType = EmploymentType.FullTime };
    public WorkloadFairnessGroupViewModel PartTime { get; set; } = new() { EmploymentType = EmploymentType.PartTime };
}

public sealed class WorkdayDetailItemViewModel
{
    public int ShiftAssignmentId { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateOnly WorkDate { get; set; }
    public int ShiftTypeId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public string ShiftTimeRange { get; set; } = string.Empty;
    public decimal PlannedHours { get; set; }
    public decimal? ActualWorkedHours { get; set; }
    public decimal SystemCalculatedHours { get; set; }
    public decimal EffectiveWorkedHours { get; set; }
    public string? WorkdayAdjustmentReason { get; set; }
    public string? AdjustmentReason => WorkdayAdjustmentReason;
    public bool HasApprovedLeave { get; set; }
    public bool IsManagerAdjusted => ActualWorkedHours.HasValue;
    public string HourSourceText => ActualWorkedHours != null
        ? "Quản lý điều chỉnh"
        : (HasApprovedLeave ? "Nghỉ đã duyệt — tự động 0 giờ" : "Theo lịch");
    public bool CanAdjust { get; set; }
    public string FormattedPlannedHours => $"{PlannedHours:0.##} h";
    public string FormattedEffectiveWorkedHours => $"{EffectiveWorkedHours:0.##} h";
}

public sealed class AdjustWorkdayHoursRequest
{
    public int SchedulePeriodId { get; set; }
    public int ShiftAssignmentId { get; set; }

    [ModelBinder(typeof(WorkdayHoursModelBinder))]
    public decimal? ActualWorkedHours { get; set; }

    public string? WorkdayAdjustmentReason { get; set; }
    public bool IsReset { get; set; }
    public int? ReturnEmployeeId { get; set; }
    public EmployeePosition? ReturnPosition { get; set; }
    public EmploymentType? ReturnEmploymentType { get; set; }
}

public sealed class EmployeeWorkReportOverviewViewModel
{
    public int SchedulePeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public List<ReportPeriodOptionViewModel> PeriodOptions { get; set; } = new();
    public WorkReportRowItemViewModel? Summary { get; set; }
}

public sealed class ReportPeriodOptionViewModel
{
    public int SchedulePeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
}

public sealed class ReportEmployeeOptionViewModel
{
    public int EmployeeId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
