using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Data;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Services.Common;
using ShiftScheduling.Services.Security;
using ShiftScheduling.ViewModels.Account;

namespace ShiftScheduling.Services.Accounts;

public sealed class AccountService : IAccountService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHashService _passwordHashService;

    public AccountService(
        ApplicationDbContext dbContext,
        IPasswordHashService passwordHashService)
    {
        _dbContext = dbContext;
        _passwordHashService = passwordHashService;
    }

    public async Task<AuthenticatedAccount?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = NormalizeUsername(username);

        var account = await _dbContext.UserAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Username == normalizedUsername,
                cancellationToken);

        if (account is null || account.IsLocked)
        {
            return null;
        }

        if (!_passwordHashService.VerifyPassword(password, account.PasswordHash))
        {
            return null;
        }

        return new AuthenticatedAccount(
            account.UserAccountId,
            account.Username,
            account.Role);
    }

    public async Task<AccountIndexViewModel> GetIndexAsync(
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.UserAccounts
            .AsNoTracking()
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

        var accounts = await query
            .OrderBy(x => x.Role)
            .ThenBy(x => x.Username)
            .Select(x => new AccountListItemViewModel
            {
                UserAccountId = x.UserAccountId,
                Username = x.Username,
                Role = x.Role,
                IsLocked = x.IsLocked,
                CreatedAt = x.CreatedAt,
                CreatedByUsername = x.CreatedByAccount != null
                    ? x.CreatedByAccount.Username
                    : null,
                EmployeeCode = x.Employee != null
                    ? x.Employee.EmployeeCode
                    : null,
                EmployeeName = x.Employee != null
                    ? x.Employee.FullName
                    : null
            })
            .ToListAsync(cancellationToken);

        return new AccountIndexViewModel
        {
            Keyword = keyword,
            Accounts = accounts
        };
    }

    public async Task<AccountCreateViewModel> GetCreateAsync(
        CancellationToken cancellationToken = default)
    {
        return new AccountCreateViewModel
        {
            Role = UserRole.Employee,
            EmployeeOptions = await GetAvailableEmployeesAsync(
                currentAccountId: null,
                cancellationToken)
        };
    }

    public async Task<ServiceResult> CreateAsync(
        AccountCreateViewModel model,
        int createdByAccountId,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = NormalizeUsername(model.Username);
        // Username phải duy nhất trong toàn hệ thống.
        var usernameExists = await _dbContext.UserAccounts
            .AnyAsync(
                x => x.Username == normalizedUsername,
                cancellationToken);

        if (usernameExists)
        {
            return ServiceResult.Failure("Tên đăng nhập đã tồn tại.");
        }

        var employeeValidation = await ValidateEmployeeLinkAsync(
            model.Role,
            model.EmployeeId,
            currentAccountId: null,
            cancellationToken);

        if (!employeeValidation.Result.Succeeded)
        {
            return employeeValidation.Result;
        }

        var account = new UserAccount
        {
            Username = normalizedUsername,
            PasswordHash = _passwordHashService.HashPassword(model.Password),
            Role = model.Role,
            IsLocked = false,
            CreatedAt = DateTime.Now,
            CreatedByAccountId = createdByAccountId
        };

        _dbContext.UserAccounts.Add(account);

        if (employeeValidation.Employee is not null)
        {
            employeeValidation.Employee.UserAccount = account;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    public async Task<AccountEditViewModel?> GetEditAsync(
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var model = await _dbContext.UserAccounts
            .AsNoTracking()
            .Where(x => x.UserAccountId == accountId)
            .Select(x => new AccountEditViewModel
            {
                UserAccountId = x.UserAccountId,
                Username = x.Username,
                Role = x.Role,
                EmployeeId = x.Employee != null
                    ? x.Employee.EmployeeId
                    : null,
                IsLocked = x.IsLocked
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (model is null)
        {
            return null;
        }

        model.EmployeeOptions = await GetAvailableEmployeesAsync(
            accountId,
            cancellationToken);

        return model;
    }

    public async Task<ServiceResult> UpdateAsync(
        int accountId,
        AccountEditViewModel model,
        int currentAccountId,
        CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.UserAccounts
            .Include(x => x.Employee)
            .SingleOrDefaultAsync(
                x => x.UserAccountId == accountId,
                cancellationToken);

        if (account is null)
        {
            return ServiceResult.Failure("Không tìm thấy tài khoản.");
        }

        var normalizedUsername = NormalizeUsername(model.Username);

        if (accountId == currentAccountId
            && !string.Equals(
                account.Username,
                normalizedUsername,
                StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult.Failure(
                "Không đổi tên đăng nhập của tài khoản đang sử dụng. Hãy đăng nhập bằng tài khoản quản trị khác để thực hiện.");
        }

        if (accountId == currentAccountId && account.Role != model.Role)
        {
            return ServiceResult.Failure(
                "Không thể tự thay đổi vai trò của tài khoản đang đăng nhập.");
        }

        var usernameExists = await _dbContext.UserAccounts
            .AnyAsync(
                x => x.UserAccountId != accountId
                    && x.Username == normalizedUsername,
                cancellationToken);

        if (usernameExists)
        {
            return ServiceResult.Failure("Tên đăng nhập đã được sử dụng.");
        }

        if (account.Role == UserRole.Admin && model.Role != UserRole.Admin)
        {
            var otherActiveAdminExists = await _dbContext.UserAccounts
                .AnyAsync(
                    x => x.UserAccountId != accountId
                        && x.Role == UserRole.Admin
                        && !x.IsLocked,
                    cancellationToken);

            if (!otherActiveAdminExists)
            {
                return ServiceResult.Failure(
                    "Hệ thống phải còn ít nhất một tài khoản quản trị đang hoạt động.");
            }
        }

        var employeeValidation = await ValidateEmployeeLinkAsync(
            model.Role,
            model.EmployeeId,
            accountId,
            cancellationToken);

        if (!employeeValidation.Result.Succeeded)
        {
            return employeeValidation.Result;
        }

        if (account.Employee is not null
            && account.Employee.EmployeeId != model.EmployeeId)
        {
            account.Employee.UserAccountId = null;
        }

        if (employeeValidation.Employee is not null)
        {
            employeeValidation.Employee.UserAccountId = accountId;
        }

        account.Username = normalizedUsername;
        account.Role = model.Role;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> SetLockedAsync(
        int accountId,
        bool isLocked,
        int currentAccountId,
        CancellationToken cancellationToken = default)
    {
        if (accountId == currentAccountId)
        {
            return ServiceResult.Failure(
                "Không thể khóa tài khoản đang đăng nhập.");
        }

        var account = await _dbContext.UserAccounts
            .SingleOrDefaultAsync(
                x => x.UserAccountId == accountId,
                cancellationToken);

        if (account is null)
        {
            return ServiceResult.Failure("Không tìm thấy tài khoản.");
        }

        if (isLocked && account.Role == UserRole.Admin)
        {
            var otherActiveAdminExists = await _dbContext.UserAccounts
                .AnyAsync(
                    x => x.UserAccountId != accountId
                        && x.Role == UserRole.Admin
                        && !x.IsLocked,
                    cancellationToken);

            if (!otherActiveAdminExists)
            {
                return ServiceResult.Failure(
                    "Không thể khóa tài khoản quản trị đang hoạt động cuối cùng.");
            }
        }

        account.IsLocked = isLocked;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    public async Task<AccountResetPasswordViewModel?> GetResetPasswordAsync(
        int accountId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserAccounts
            .AsNoTracking()
            .Where(x => x.UserAccountId == accountId)
            .Select(x => new AccountResetPasswordViewModel
            {
                UserAccountId = x.UserAccountId,
                Username = x.Username
            })
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ServiceResult> ResetPasswordAsync(
        int accountId,
        AccountResetPasswordViewModel model,
        CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.UserAccounts
            .SingleOrDefaultAsync(
                x => x.UserAccountId == accountId,
                cancellationToken);

        if (account is null)
        {
            return ServiceResult.Failure("Không tìm thấy tài khoản.");
        }

        account.PasswordHash = _passwordHashService.HashPassword(model.NewPassword);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    private async Task<IReadOnlyList<AccountEmployeeOptionViewModel>> GetAvailableEmployeesAsync(
        int? currentAccountId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Employees
            .AsNoTracking()
            .Where(x => x.IsActive
                && (x.UserAccountId == null
                    || x.UserAccountId == currentAccountId))
            .OrderBy(x => x.EmployeeCode)
            .Select(x => new AccountEmployeeOptionViewModel
            {
                EmployeeId = x.EmployeeId,
                EmployeeCode = x.EmployeeCode,
                FullName = x.FullName
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<(ServiceResult Result, Employee? Employee)> ValidateEmployeeLinkAsync(
        UserRole role,
        int? employeeId,
        int? currentAccountId,
        CancellationToken cancellationToken)
    {
        if (role != UserRole.Employee && employeeId.HasValue)
        {
            return (
                ServiceResult.Failure(
                    "Chỉ tài khoản có vai trò Nhân viên mới được liên kết với hồ sơ nhân viên."),
                null);
        }

        if (!employeeId.HasValue)
        {
            return (ServiceResult.Success(), null);
        }

        var employee = await _dbContext.Employees
            .SingleOrDefaultAsync(
                x => x.EmployeeId == employeeId.Value,
                cancellationToken);

        if (employee is null || !employee.IsActive)
        {
            return (
                ServiceResult.Failure(
                    "Không tìm thấy hồ sơ nhân viên đang hoạt động."),
                null);
        }
        // Một nhân viên chỉ được liên kết với tối đa một tài khoản.
        if (employee.UserAccountId.HasValue
            && employee.UserAccountId != currentAccountId)
        {
            return (
                ServiceResult.Failure(
                    "Hồ sơ nhân viên đã được liên kết với tài khoản khác."),
                null);
        }

        return (ServiceResult.Success(), employee);
    }

    private static string NormalizeUsername(string username)
        => username.Trim().ToLowerInvariant();
}
