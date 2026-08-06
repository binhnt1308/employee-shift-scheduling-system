using ShiftScheduling.Models.Enums;
using ShiftScheduling.Services.Common;
using ShiftScheduling.ViewModels.Account;

namespace ShiftScheduling.Services.Accounts;

public sealed record AuthenticatedAccount(
    int UserAccountId,
    string Username,
    UserRole Role);

public interface IAccountService
{
    Task<AuthenticatedAccount?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task<AccountIndexViewModel> GetIndexAsync(
        string? keyword,
        CancellationToken cancellationToken = default);

    Task<AccountCreateViewModel> GetCreateAsync(
        CancellationToken cancellationToken = default);

    Task<ServiceResult> CreateAsync(
        AccountCreateViewModel model,
        int createdByAccountId,
        CancellationToken cancellationToken = default);

    Task<AccountEditViewModel?> GetEditAsync(
        int accountId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(
        int accountId,
        AccountEditViewModel model,
        int currentAccountId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> SetLockedAsync(
        int accountId,
        bool isLocked,
        int currentAccountId,
        CancellationToken cancellationToken = default);

    Task<AccountResetPasswordViewModel?> GetResetPasswordAsync(
        int accountId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> ResetPasswordAsync(
        int accountId,
        AccountResetPasswordViewModel model,
        CancellationToken cancellationToken = default);
}
