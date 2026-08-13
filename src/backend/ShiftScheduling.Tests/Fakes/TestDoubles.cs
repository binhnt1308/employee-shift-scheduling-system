using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.Accounts.Repositories;
using ShiftScheduling.Features.Authentication.Repositories;
using ShiftScheduling.Features.Employees.Repositories;
using ShiftScheduling.Features.ShiftTypes.Repositories;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Tests.Fakes;

internal sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; init; }
    public DateOnly TodayInVietnam { get; init; } = new(2026, 8, 6);

    public FakeClock(DateTime? utcNow = null)
    {
        UtcNow = utcNow ?? new DateTime(2026, 8, 6, 4, 0, 0, DateTimeKind.Utc);
        TodayInVietnam = DateOnly.FromDateTime(UtcNow.AddHours(7));
    }

    public DateTime ToVietnamTime(DateTime utcDateTime)
    {
        return DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc).AddHours(7);
    }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}

internal sealed class FakeAuditLogRepository : IAuditLogRepository
{
    public List<AuditLog> Items { get; } = [];

    public void Add(AuditLog auditLog)
    {
        Items.Add(auditLog);
    }
}

internal sealed class FakePasswordHashService : IPasswordHashService
{
    public string HashPassword(string password) => $"hashed:{password}";
    public bool VerifyPassword(string password, string encodedHash) => encodedHash == $"hashed:{password}";
}

internal sealed class FakeAuthenticationRepository : IAuthenticationRepository
{
    public List<UserAccount> Accounts { get; } = [];

    public Task<UserAccount?> GetByUsernameAsync(
        string normalizedUsername,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Accounts.SingleOrDefault(x => x.Username == normalizedUsername));
    }
}

internal sealed class FakeEmployeeRepository : IEmployeeRepository
{
    public List<Employee> Employees { get; } = [];
    public bool EmployeeCodeExists { get; set; }
    public bool EmailExists { get; set; }

    public Task<PagedResult<Employee>> GetPagedAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PagedResult<Employee>(Employees, page, pageSize, Employees.Count));
    }

    public Task<Employee?> GetByIdAsync(
        int employeeId,
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Employees.SingleOrDefault(x => x.EmployeeId == employeeId));
    }

    public Task<bool> EmployeeCodeExistsAsync(
        string normalizedEmployeeCode,
        int? excludedEmployeeId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(EmployeeCodeExists);
    }

    public Task<bool> EmailExistsAsync(
        string normalizedEmail,
        int? excludedEmployeeId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(EmailExists);
    }

    public Task<List<Employee>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Employees.Where(x => x.IsActive).ToList());
    }

    public Task<Employee?> GetByAccountIdAsync(
        int accountId,
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Employees.FirstOrDefault(x => x.UserAccountId == accountId));
    }

    public void Add(Employee employee)
    {
        Employees.Add(employee);
    }
}

internal sealed class FakeShiftTypeRepository : IShiftTypeRepository
{
    public List<ShiftType> ShiftTypes { get; } = [];
    public bool DisplayOrderExists { get; set; }
    public bool Overlaps { get; set; }

    public Task<IReadOnlyList<ShiftType>> GetAllAsync(
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ShiftType>>(ShiftTypes);
    }

    public Task<ShiftType?> GetByIdAsync(
        int shiftTypeId,
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ShiftTypes.SingleOrDefault(x => x.ShiftTypeId == shiftTypeId));
    }

    public Task<bool> DisplayOrderExistsAsync(
        int displayOrder,
        int excludedShiftTypeId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(DisplayOrderExists);
    }

    public Task<bool> OverlapsAsync(
        TimeOnly startTime,
        TimeOnly endTime,
        int excludedShiftTypeId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Overlaps);
    }
}

internal sealed class FakeAccountRepository : IAccountRepository
{
    public List<UserAccount> Accounts { get; } = [];
    public List<Employee> Employees { get; } = [];
    public bool UsernameExists { get; set; }
    public bool HasOtherActiveAdmin { get; set; } = true;
    public bool EmployeeLinked { get; set; }

    public Task<UserAccount?> GetByIdAsync(
        int accountId,
        bool includeEmployee,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Accounts.SingleOrDefault(x => x.UserAccountId == accountId));
    }

    public Task<PagedResult<UserAccount>> GetPagedAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PagedResult<UserAccount>(Accounts, page, pageSize, Accounts.Count));
    }

    public Task<bool> UsernameExistsAsync(
        string normalizedUsername,
        int? excludedAccountId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(UsernameExists);
    }

    public Task<bool> HasOtherActiveAdminAsync(
        int excludedAccountId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HasOtherActiveAdmin);
    }

    public Task<IReadOnlyList<Employee>> GetAvailableEmployeesAsync(
        int? currentAccountId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Employee>>(Employees);
    }

    public Task<Employee?> GetEmployeeByIdAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Employees.SingleOrDefault(x => x.EmployeeId == employeeId));
    }

    public Task<bool> IsEmployeeLinkedToOtherAccountAsync(
        int employeeId,
        int? currentAccountId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(EmployeeLinked);
    }

    public void Add(UserAccount account)
    {
        Accounts.Add(account);
    }
}
