using ShiftScheduling.Models.Results;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Accounts.Services;

public interface IAccountService
{
    /// <summary>
    /// Lấy danh sách tài khoản có phân trang và tìm kiếm.
    /// </summary>
    Task<AccountIndexViewModel> GetIndexAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo dữ liệu khởi tạo cho màn hình thêm tài khoản.
    /// </summary>
    Task<AccountCreateViewModel> GetCreateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo tài khoản, băm mật khẩu và ghi nhật ký thao tác.
    /// </summary>
    Task<ServiceResult> CreateAsync(
        AccountCreateViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy tài khoản và danh sách nhân viên có thể liên kết để cập nhật.
    /// </summary>
    Task<AccountEditViewModel?> GetEditAsync(
        int accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật tên đăng nhập, vai trò và hồ sơ nhân viên liên kết.
    /// </summary>
    Task<ServiceResult> UpdateAsync(
        int accountId,
        AccountEditViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Khóa hoặc mở khóa tài khoản và bảo vệ tài khoản quản trị cuối cùng.
    /// </summary>
    Task<ServiceResult> SetLockedAsync(
        int accountId,
        bool isLocked,
        int actorAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy dữ liệu hiển thị cho màn hình đặt lại mật khẩu.
    /// </summary>
    Task<AccountResetPasswordViewModel?> GetResetPasswordAsync(
        int accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đặt lại mật khẩu đã băm và ghi nhật ký không chứa mật khẩu.
    /// </summary>
    Task<ServiceResult> ResetPasswordAsync(
        int accountId,
        AccountResetPasswordViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);
}
