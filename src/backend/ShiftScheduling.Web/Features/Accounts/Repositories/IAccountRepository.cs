using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Accounts.Repositories;

// Khai báo các thao tác dữ liệu cần cho nghiệp vụ tài khoản và phân quyền; Service giữ quyền quyết định business rule.
public interface IAccountRepository
{

    Task<UserAccount?> GetByIdAsync(
        int accountId,
        bool includeEmployee,
        CancellationToken cancellationToken = default);

    Task<PagedResult<UserAccount>> GetPagedAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<bool> UsernameExistsAsync(
        string normalizedUsername,
        int? excludedAccountId,
        CancellationToken cancellationToken = default);

    Task<bool> HasOtherActiveAdminAsync(
        int excludedAccountId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Employee>> GetAvailableEmployeesAsync(
        int? currentAccountId,
        CancellationToken cancellationToken = default);

    Task<Employee?> GetEmployeeByIdAsync(
        int employeeId,
        CancellationToken cancellationToken = default);

    Task<bool> IsEmployeeLinkedToOtherAccountAsync(
        int employeeId,
        int? currentAccountId,
        CancellationToken cancellationToken = default);

    void Add(UserAccount account);
}
