using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Models.ViewModels;

public sealed class DashboardViewModel
{
    public int AccountCount { get; init; }
    public int EmployeeCount { get; init; }
    public int ShiftTypeCount { get; init; }
    public IReadOnlyList<ShiftType> ShiftTypes { get; init; } = Array.Empty<ShiftType>();
}
