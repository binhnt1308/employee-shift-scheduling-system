using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Accounts.Services;

public sealed partial class AccountService
{
    private async Task<(ServiceResult Result, Employee? Employee)> ValidateNewAccountAsync(
        AccountCreateViewModel model,
        string normalizedUsername,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(typeof(UserRole), model.Role))
        {
            return (ServiceResult.Failure(ErrorCodes.InvalidEnumValue, "Vai trò không hợp lệ."), null);
        }

        if (await _accountRepository.UsernameExistsAsync(normalizedUsername, null, cancellationToken))
        {
            return (ServiceResult.Failure(ErrorCodes.DuplicateUsername, "Tên đăng nhập đã tồn tại."), null);
        }

        return await ValidateEmployeeLinkAsync(model.Role, model.EmployeeId, null, cancellationToken);
    }

    private async Task<(ServiceResult Result, Employee? Employee)> ValidateAccountUpdateAsync(
        UserAccount account,
        AccountEditViewModel model,
        string normalizedUsername,
        int actorAccountId,
        CancellationToken cancellationToken)
    {
        var selfValidation = ValidateSelfUpdate(account, model, normalizedUsername, actorAccountId);
        if (!selfValidation.Succeeded)
        {
            return (selfValidation, null);
        }

        if (await _accountRepository.UsernameExistsAsync(normalizedUsername, account.UserAccountId, cancellationToken))
        {
            return (ServiceResult.Failure(
                ErrorCodes.DuplicateUsername,
                "Tên đăng nhập đã được sử dụng."), null);
        }

        var adminValidation = await ValidateRoleChangeAsync(account, model.Role, cancellationToken);
        return !adminValidation.Succeeded
            ? (adminValidation, null)
            : await ValidateEmployeeLinkAsync(model.Role, model.EmployeeId, account.UserAccountId, cancellationToken);
    }

    private static ServiceResult ValidateSelfUpdate(
        UserAccount account,
        AccountEditViewModel model,
        string normalizedUsername,
        int actorAccountId)
    {
        // Quản trị viên có thể chỉnh tài khoản khác, nhưng tài khoản đang đăng nhập không được tự đổi
        // danh tính hoặc quyền của chính mình qua luồng quản trị này.
        if (account.UserAccountId != actorAccountId)
        {
            return ServiceResult.Success();
        }

        if (!string.Equals(account.Username, normalizedUsername, StringComparison.OrdinalIgnoreCase)
            || account.Role != model.Role)
        {
            return ServiceResult.Failure(
                ErrorCodes.SelfAccountChange,
                "Không thể tự đổi tên đăng nhập hoặc vai trò của tài khoản đang sử dụng.");
        }

        return ServiceResult.Success();
    }

    private async Task<ServiceResult> ValidateRoleChangeAsync(
        UserAccount account,
        UserRole newRole,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(typeof(UserRole), newRole))
        {
            return ServiceResult.Failure(ErrorCodes.InvalidEnumValue, "Vai trò không hợp lệ.");
        }

        // Chỉ cần kiểm tra "Admin cuối cùng" khi một Admin đang bị hạ sang vai trò khác.
        if (account.Role != UserRole.Admin || newRole == UserRole.Admin)
        {
            return ServiceResult.Success();
        }

        var hasOtherAdmin = await _accountRepository.HasOtherActiveAdminAsync(
            account.UserAccountId,
            cancellationToken);
        return hasOtherAdmin
            ? ServiceResult.Success()
            : ServiceResult.Failure(
                ErrorCodes.LastActiveAdmin,
                "Hệ thống phải còn ít nhất một tài khoản quản trị đang hoạt động.");
    }

    private async Task<ServiceResult> ValidateAdminAvailabilityAsync(
        UserAccount account,
        bool isLocked,
        CancellationToken cancellationToken)
    {
        if (!isLocked || account.Role != UserRole.Admin)
        {
            return ServiceResult.Success();
        }

        var hasOtherAdmin = await _accountRepository.HasOtherActiveAdminAsync(
            account.UserAccountId,
            cancellationToken);
        return hasOtherAdmin
            ? ServiceResult.Success()
            : ServiceResult.Failure(
                ErrorCodes.LastActiveAdmin,
                "Không thể khóa tài khoản quản trị đang hoạt động cuối cùng.");
    }

    private async Task<(ServiceResult Result, Employee? Employee)> ValidateEmployeeLinkAsync(
        UserRole role,
        int? employeeId,
        int? currentAccountId,
        CancellationToken cancellationToken)
    {
        // BR01: tài khoản nhân viên phải gắn đúng một hồ sơ nhân viên; các vai trò khác không được gắn hồ sơ.
        if (role != UserRole.Employee)
        {
            return employeeId.HasValue
                ? (ServiceResult.Failure(
                    ErrorCodes.InvalidEmployeeLink,
                    "Chỉ tài khoản vai trò Nhân viên mới được liên kết hồ sơ nhân viên."), null)
                : (ServiceResult.Success(), null);
        }

        if (!employeeId.HasValue)
        {
            return (ServiceResult.Failure(
                ErrorCodes.InvalidEmployeeLink,
                "Tài khoản Nhân viên phải liên kết với một hồ sơ nhân viên."), null);
        }

        var employee = await _accountRepository.GetEmployeeByIdAsync(employeeId.Value, cancellationToken);
        if (employee is null || !employee.IsActive)
        {
            return (ServiceResult.Failure(
                ErrorCodes.InvalidEmployeeLink,
                "Hồ sơ nhân viên không tồn tại hoặc đã ngừng hoạt động."), null);
        }

        var linked = await _accountRepository.IsEmployeeLinkedToOtherAccountAsync(
            employeeId.Value,
            currentAccountId,
            cancellationToken);
        return linked
            ? (ServiceResult.Failure(
                ErrorCodes.InvalidEmployeeLink,
                "Hồ sơ nhân viên đã được liên kết với tài khoản khác."), null)
            : (ServiceResult.Success(), employee);
    }

    private UserAccount BuildNewAccount(
        AccountCreateViewModel model,
        string normalizedUsername,
        int actorAccountId)
    {
        return new UserAccount
        {
            Username = normalizedUsername,
            PasswordHash = _passwordHashService.HashPassword(model.Password),
            Role = model.Role,
            IsLocked = false,
            CreatedAt = _clock.UtcNow,
            CreatedByAccountId = actorAccountId
        };
    }

    private static void ApplyAccountUpdate(
        UserAccount account,
        AccountEditViewModel model,
        string normalizedUsername,
        Employee? linkedEmployee)
    {
        // Gỡ liên kết cũ trước khi gắn hồ sơ mới để một nhân viên không bị giữ đồng thời bởi hai quan hệ.
        if (account.Employee is not null && account.Employee.EmployeeId != model.EmployeeId)
        {
            account.Employee.UserAccountId = null;
        }

        if (linkedEmployee is not null)
        {
            linkedEmployee.UserAccountId = account.UserAccountId;
        }

        account.Username = normalizedUsername;
        account.Role = model.Role;
    }

    private static void LinkEmployee(Employee? employee, UserAccount account)
    {
        if (employee is not null)
        {
            employee.UserAccount = account;
        }
    }

    private async Task<IReadOnlyList<AccountEmployeeOptionViewModel>> GetEmployeeOptionsAsync(
        int? currentAccountId,
        CancellationToken cancellationToken)
    {
        var employees = await _accountRepository.GetAvailableEmployeesAsync(currentAccountId, cancellationToken);
        return employees.Select(x => new AccountEmployeeOptionViewModel
        {
            EmployeeId = x.EmployeeId,
            EmployeeCode = x.EmployeeCode,
            FullName = x.FullName
        }).ToArray();
    }

    private void AddAuditLog(
        int actorAccountId,
        string action,
        UserAccount account,
        object? previousValues,
        object? newValues)
    {
        _auditLogRepository.Add(AuditLogFactory.Create(
            actorAccountId,
            action,
            nameof(UserAccount),
            account.UserAccountId == 0 ? account.Username : account.UserAccountId.ToString(),
            previousValues,
            newValues,
            _clock.UtcNow));
    }

    private static object BuildAuditValues(UserAccount account, int? employeeId)
    {
        return new
        {
            account.Username,
            account.Role,
            account.IsLocked,
            EmployeeId = employeeId
        };
    }

    private AccountListItemViewModel MapListItem(UserAccount account)
    {
        return new AccountListItemViewModel
        {
            UserAccountId = account.UserAccountId,
            Username = account.Username,
            Role = account.Role,
            IsLocked = account.IsLocked,
            CreatedAt = _clock.ToVietnamTime(account.CreatedAt),
            CreatedByUsername = account.CreatedByAccount?.Username,
            EmployeeCode = account.Employee?.EmployeeCode,
            EmployeeName = account.Employee?.FullName
        };
    }

    private static string NormalizeUsername(string username)
    {
        return username.Trim().ToLowerInvariant();
    }
}
