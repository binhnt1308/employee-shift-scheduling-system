using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Models.ViewModels;

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
}
