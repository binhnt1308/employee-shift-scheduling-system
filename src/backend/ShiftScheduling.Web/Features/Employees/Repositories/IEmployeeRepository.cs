using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Employees.Repositories;

public interface IEmployeeRepository
{
    Task<PagedResult<Employee>> GetPagedAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Employee?> GetByIdAsync(
        int employeeId,
        bool asNoTracking,
        CancellationToken cancellationToken = default);

    Task<bool> EmployeeCodeExistsAsync(
        string normalizedEmployeeCode,
        int? excludedEmployeeId,
        CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(
        string normalizedEmail,
        int? excludedEmployeeId,
        CancellationToken cancellationToken = default);

    Task<List<Employee>> GetAllActiveAsync(
        CancellationToken cancellationToken = default);

    Task<Employee?> GetByAccountIdAsync(
        int accountId,
        bool asNoTracking,
        CancellationToken cancellationToken = default);

    void Add(Employee employee);
}
