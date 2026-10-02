using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Models.ViewModels;

// Chứa các ViewModel dùng để trao đổi dữ liệu giữa Controller và View của chức năng Dashboard tổng quan.
public sealed class DashboardViewModel
{
    public int AccountCount { get; init; }
    public int EmployeeCount { get; init; }
    public int ShiftTypeCount { get; init; }
    public IReadOnlyList<ShiftType> ShiftTypes { get; init; } = Array.Empty<ShiftType>();
    public string? TargetPeriodName { get; init; }
    public string? TargetPeriodDateRange { get; init; }
    public string ScheduleStatusDisplay { get; init; } = "Chưa có kỳ lịch";
    public bool NeedsAttention { get; init; }
    public string? AttentionMessage { get; init; }
    public bool HasSchedule { get; init; }
    public bool HasMySchedule { get; init; }
    public string EmployeeScheduleStatusDisplay { get; init; } = "Chưa có kỳ lịch";
    public string EmployeePeriodLabel { get; init; } = "Chưa có kỳ lịch";
    public string? EmployeeFullName { get; init; }
    public bool IsRegistrationOpen { get; init; }
    public string EmployeeScheduleBadgeClass { get; init; } = "badge-info-custom";
    public IReadOnlyList<EmployeeDashboardDayScheduleViewModel> EmployeeWeekSchedule { get; init; } = Array.Empty<EmployeeDashboardDayScheduleViewModel>();
    public int EmployeeTotalAssignedShifts { get; init; }
}

public sealed class EmployeeDashboardDayScheduleViewModel
{
    public DateOnly Date { get; init; }
    public string DayOfWeekName { get; init; } = string.Empty;
    public string DateFormatted { get; init; } = string.Empty;
    public bool IsToday { get; init; }
    public IReadOnlyList<EmployeeDashboardShiftItemViewModel> Shifts { get; init; } = Array.Empty<EmployeeDashboardShiftItemViewModel>();
}

public sealed class EmployeeDashboardShiftItemViewModel
{
    public string ShiftName { get; init; } = string.Empty;
    public string ShiftCode { get; init; } = string.Empty;
    public string ShiftHours { get; init; } = string.Empty;
    public bool HasApprovedLeave { get; init; }
}
