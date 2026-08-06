using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.ViewModels.Employee;

public class EmployeeListItemViewModel
{
    public int EmployeeId { get; init; }
    public string EmployeeCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public EmployeePosition Position { get; init; }
    public EmploymentType EmploymentType { get; init; }
    public EmployeeLevel Level { get; init; }
    public bool IsActive { get; init; }

    public int ExpectedWeeklyHours =>
        EmploymentType == EmploymentType.FullTime ? 48 : 20;
}
