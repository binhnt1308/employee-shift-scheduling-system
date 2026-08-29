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
    /// Xac thuc tai khoan dang hoat dong bang ten dang nhap va mat khau.
    /// </summary>
    /// <param name="username">Ten dang nhap do nguoi dung nhap.</param>
    /// <param name="password">Mat khau dang ro chi ton tai trong bo nho luc xac thuc.</param>
    /// <returns>Thong tin toi thieu de tao cookie, hoac null neu xac thuc that bai.</returns>
    Task<AuthenticatedAccount?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);
}
