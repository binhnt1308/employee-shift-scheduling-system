using ShiftScheduling.Services.Common;
using ShiftScheduling.ViewModels.Employee;

namespace ShiftScheduling.Services.Employees;

public interface IEmployeeService
{
    Task<EmployeeIndexViewModel> GetIndexAsync(
        string? keyword,
        CancellationToken cancellationToken = default);

    Task<EmployeeFormViewModel?> GetFormAsync(
        int employeeId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> CreateAsync(
        EmployeeFormViewModel model,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(
        int employeeId,
        EmployeeFormViewModel model,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> SetActiveAsync(
        int employeeId,
        bool isActive,
        CancellationToken cancellationToken = default);
}
