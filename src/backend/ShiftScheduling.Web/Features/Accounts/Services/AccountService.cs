using Microsoft.Extensions.Logging;
using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.Accounts.Repositories;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Accounts.Services;

public sealed partial class AccountService : IAccountService
{
    private const int MinPageSize = 5;
    private const int MaxPageSize = 50;

    private readonly IAccountRepository _accountRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        IAccountRepository accountRepository,
        IAuditLogRepository auditLogRepository,
        IPasswordHashService passwordHashService,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<AccountService> logger)
    {
        _accountRepository = accountRepository;
        _auditLogRepository = auditLogRepository;
        _passwordHashService = passwordHashService;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AccountIndexViewModel> GetIndexAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, MinPageSize, MaxPageSize);
        var result = await _accountRepository.GetPagedAsync(
            keyword,
            normalizedPage,
            normalizedPageSize,
            cancellationToken);

        var items = result.Items.Select(MapListItem).ToArray();
        return new AccountIndexViewModel
        {
            Keyword = keyword,
            Accounts = new PagedResult<AccountListItemViewModel>(
                items,
                result.Page,
                result.PageSize,
                result.TotalItems)
        };
    }

    /// <inheritdoc />
    public async Task<AccountCreateViewModel> GetCreateAsync(CancellationToken cancellationToken = default)
    {
        return new AccountCreateViewModel
        {
            Role = UserRole.Employee,
            EmployeeOptions = await GetEmployeeOptionsAsync(null, cancellationToken)
        };
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(
        AccountCreateViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = NormalizeUsername(model.Username);
        // BR01: Chi quan tri vien duoc tao tai khoan va moi tai khoan chi co mot vai tro hop le.
        var validation = await ValidateNewAccountAsync(model, normalizedUsername, cancellationToken);
        if (!validation.Result.Succeeded)
        {
            return validation.Result;
        }

        var account = BuildNewAccount(model, normalizedUsername, actorAccountId);
        _accountRepository.Add(account);
        LinkEmployee(validation.Employee, account);
        AddAuditLog(actorAccountId, "CREATE_ACCOUNT", account, null, BuildAuditValues(account, model.EmployeeId));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Account created. ActorAccountId: {ActorAccountId}; Username: {Username}; Role: {Role}",
            actorAccountId,
            account.Username,
            account.Role);
        return ServiceResult.Success();
    }

    /// <inheritdoc />
    public async Task<AccountEditViewModel?> GetEditAsync(
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(accountId, true, cancellationToken);
        if (account is null)
        {
            return null;
        }

        return new AccountEditViewModel
        {
            UserAccountId = account.UserAccountId,
            Username = account.Username,
            Role = account.Role,
            EmployeeId = account.Employee?.EmployeeId,
            IsLocked = account.IsLocked,
            EmployeeOptions = await GetEmployeeOptionsAsync(accountId, cancellationToken)
        };
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateAsync(
        int accountId,
        AccountEditViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(accountId, true, cancellationToken);
        if (account is null)
        {
            return ServiceResult.Failure(ErrorCodes.AccountNotFound, "Không tìm thấy tài khoản.");
        }

        var normalizedUsername = NormalizeUsername(model.Username);
        var validation = await ValidateAccountUpdateAsync(
            account,
            model,
            normalizedUsername,
            actorAccountId,
            cancellationToken);
        if (!validation.Result.Succeeded)
        {
            return validation.Result;
        }

        var previousValues = BuildAuditValues(account, account.Employee?.EmployeeId);
        ApplyAccountUpdate(account, model, normalizedUsername, validation.Employee);
        AddAuditLog(
            actorAccountId,
            "UPDATE_ACCOUNT",
            account,
            previousValues,
            BuildAuditValues(account, model.EmployeeId));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Account updated. ActorAccountId: {ActorAccountId}; AccountId: {AccountId}",
            actorAccountId,
            accountId);
        return ServiceResult.Success();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> SetLockedAsync(
        int accountId,
        bool isLocked,
        int actorAccountId,
        CancellationToken cancellationToken = default)
    {
        if (accountId == actorAccountId)
        {
            return ServiceResult.Failure(ErrorCodes.SelfAccountChange, "Không thể khóa tài khoản đang đăng nhập.");
        }

        var account = await _accountRepository.GetByIdAsync(accountId, false, cancellationToken);
        if (account is null)
        {
            return ServiceResult.Failure(ErrorCodes.AccountNotFound, "Không tìm thấy tài khoản.");
        }

        var adminValidation = await ValidateAdminAvailabilityAsync(account, isLocked, cancellationToken);
        if (!adminValidation.Succeeded)
        {
            return adminValidation;
        }

        var previousValues = new { account.IsLocked };
        account.IsLocked = isLocked;
        AddAuditLog(actorAccountId, "SET_ACCOUNT_LOCK", account, previousValues, new { account.IsLocked });
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Account lock changed. ActorAccountId: {ActorAccountId}; AccountId: {AccountId}; IsLocked: {IsLocked}",
            actorAccountId,
            accountId,
            isLocked);
        return ServiceResult.Success();
    }

    /// <inheritdoc />
    public async Task<AccountResetPasswordViewModel?> GetResetPasswordAsync(
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(accountId, false, cancellationToken);
        return account is null
            ? null
            : new AccountResetPasswordViewModel
            {
                UserAccountId = account.UserAccountId,
                Username = account.Username
            };
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ResetPasswordAsync(
        int accountId,
        AccountResetPasswordViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(accountId, false, cancellationToken);
        if (account is null)
        {
            return ServiceResult.Failure(ErrorCodes.AccountNotFound, "Không tìm thấy tài khoản.");
        }

        account.PasswordHash = _passwordHashService.HashPassword(model.NewPassword);
        AddAuditLog(actorAccountId, "RESET_ACCOUNT_PASSWORD", account, null, new { PasswordChanged = true });
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Account password reset. ActorAccountId: {ActorAccountId}; AccountId: {AccountId}",
            actorAccountId,
            accountId);
        return ServiceResult.Success();
    }

}
