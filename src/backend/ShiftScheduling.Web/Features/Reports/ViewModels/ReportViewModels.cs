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
