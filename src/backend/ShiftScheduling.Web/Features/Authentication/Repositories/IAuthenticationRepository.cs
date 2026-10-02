using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Features.Authentication.Repositories;

// Khai báo các thao tác dữ liệu cần cho nghiệp vụ đăng nhập và xác thực; Service giữ quyền quyết định business rule.
public interface IAuthenticationRepository
{
    Task<UserAccount?> GetByUsernameAsync(
        string normalizedUsername,
        CancellationToken cancellationToken = default);
}
