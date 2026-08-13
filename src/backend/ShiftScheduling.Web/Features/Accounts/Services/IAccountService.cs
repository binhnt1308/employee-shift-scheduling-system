using ShiftScheduling.Models.Results;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Accounts.Services;

public interface IAccountService
{
    /// <summary>
    /// Lay danh sach tai khoan co phan trang va tim kiem.
    /// </summary>
    Task<AccountIndexViewModel> GetIndexAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tao du lieu khoi tao cho man hinh them tai khoan.
    /// </summary>
    Task<AccountCreateViewModel> GetCreateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tao tai khoan, bam mat khau va ghi nhat ky thao tac.
    /// </summary>
    Task<ServiceResult> CreateAsync(
        AccountCreateViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lay tai khoan va danh sach nhan vien co the lien ket de cap nhat.
    /// </summary>
    Task<AccountEditViewModel?> GetEditAsync(
        int accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cap nhat ten dang nhap, vai tro va ho so nhan vien lien ket.
    /// </summary>
    Task<ServiceResult> UpdateAsync(
        int accountId,
        AccountEditViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Khoa hoac mo khoa tai khoan va bao ve tai khoan quan tri cuoi cung.
    /// </summary>
    Task<ServiceResult> SetLockedAsync(
        int accountId,
        bool isLocked,
        int actorAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lay du lieu hien thi cho man hinh dat lai mat khau.
    /// </summary>
    Task<AccountResetPasswordViewModel?> GetResetPasswordAsync(
        int accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dat lai mat khau da bam va ghi nhat ky khong chua mat khau.
    /// </summary>
    Task<ServiceResult> ResetPasswordAsync(
        int accountId,
        AccountResetPasswordViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);
}
