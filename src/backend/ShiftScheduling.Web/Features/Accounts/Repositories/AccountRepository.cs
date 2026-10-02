using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Accounts.Repositories;

// Triển khai truy vấn EF Core cho tài khoản và phân quyền; chỉ chuẩn bị dữ liệu và tracking theo nhu cầu của Service.
public sealed class AccountRepository : IAccountRepository
{
    private readonly ApplicationDbContext _dbContext;

    public AccountRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UserAccount?> GetByIdAsync(
        int accountId,
        bool includeEmployee,
        CancellationToken cancellationToken = default)
    {
        IQueryable<UserAccount> query = _dbContext.UserAccounts;
        if (includeEmployee)
        {
            query = query.Include(x => x.Employee);
        }

        return query.SingleOrDefaultAsync(x => x.UserAccountId == accountId, cancellationToken);
    }

    public async Task<PagedResult<UserAccount>> GetPagedAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.UserAccounts
            .AsNoTracking()
            .Include(x => x.CreatedByAccount)
            .Include(x => x.Employee)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var searchText = keyword.Trim();
            query = query.Where(x =>
                x.Username.Contains(searchText)
                || (x.Employee != null
                    && (x.Employee.EmployeeCode.Contains(searchText)
                        || x.Employee.FullName.Contains(searchText))));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Role)
            .ThenBy(x => x.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<UserAccount>(items, page, pageSize, totalItems);
    }

    public Task<bool> UsernameExistsAsync(
        string normalizedUsername,
        int? excludedAccountId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.UserAccounts.AnyAsync(
            x => x.Username == normalizedUsername
                && (!excludedAccountId.HasValue || x.UserAccountId != excludedAccountId.Value),
            cancellationToken);
    }

    public Task<bool> HasOtherActiveAdminAsync(
        int excludedAccountId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.UserAccounts.AnyAsync(
            x => x.UserAccountId != excludedAccountId
                && x.Role == UserRole.Admin
                && !x.IsLocked,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Employee>> GetAvailableEmployeesAsync(
        int? currentAccountId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Employees
            .AsNoTracking()
            .Where(x => x.IsActive
                && (x.UserAccountId == null || x.UserAccountId == currentAccountId))
            .OrderBy(x => x.EmployeeCode)
            .ToListAsync(cancellationToken);
    }

    public Task<Employee?> GetEmployeeByIdAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Employees.SingleOrDefaultAsync(
            x => x.EmployeeId == employeeId,
            cancellationToken);
    }

    public Task<bool> IsEmployeeLinkedToOtherAccountAsync(
        int employeeId,
        int? currentAccountId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Employees.AnyAsync(
            x => x.EmployeeId == employeeId
                && x.UserAccountId != null
                && x.UserAccountId != currentAccountId,
            cancellationToken);
    }

    public void Add(UserAccount account)
    {
        _dbContext.UserAccounts.Add(account);
    }
}
