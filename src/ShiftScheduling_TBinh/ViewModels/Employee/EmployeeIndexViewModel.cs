namespace ShiftScheduling.ViewModels.Employee;

public class EmployeeIndexViewModel
{
    public string? Keyword { get; init; }
    public IReadOnlyList<EmployeeListItemViewModel> Employees { get; init; }
        = Array.Empty<EmployeeListItemViewModel>();
}
