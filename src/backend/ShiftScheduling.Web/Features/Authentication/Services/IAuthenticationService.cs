using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Authentication.Services;

public sealed record AuthenticatedAccount(
    int UserAccountId,
    string Username,
    UserRole Role,
    string? FullName = null,
    string? EmployeeCode = null,
    EmploymentType? EmploymentType = null,
    EmployeePosition? Position = null,
    EmployeeLevel? Level = null);

public interface IAuthenticationService
{
    /// <summary>
    /// Xác thực tài khoản đang hoạt động bằng tên đăng nhập và mật khẩu.
    /// </summary>
    /// <param name="username">Tên đăng nhập do người dùng nhập.</param>
    /// <param name="password">Mật khẩu dạng rõ chỉ tồn tại trong bộ nhớ trong lúc xác thực.</param>
    /// <returns>Thông tin tối thiểu để tạo cookie, hoặc null nếu xác thực thất bại.</returns>
    Task<AuthenticatedAccount?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);
}
