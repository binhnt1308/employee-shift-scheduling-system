using ShiftScheduling.Common.Security;
using ShiftScheduling.Features.Authentication.Repositories;

namespace ShiftScheduling.Features.Authentication.Services;

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IAuthenticationRepository _authenticationRepository;
    private readonly IPasswordHashService _passwordHashService;

    public AuthenticationService(
        IAuthenticationRepository authenticationRepository,
        IPasswordHashService passwordHashService)
    {
        _authenticationRepository = authenticationRepository;
        _passwordHashService = passwordHashService;
    }

    /// <inheritdoc />
    public async Task<AuthenticatedAccount?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = username.Trim().ToLowerInvariant();
        var account = await _authenticationRepository.GetByUsernameAsync(
            normalizedUsername,
            cancellationToken);

        // BR01: Tài khoản bị khóa không được đăng nhập vào hệ thống.
        if (account is null || account.IsLocked)
        {
            return null;
        }

        return _passwordHashService.VerifyPassword(password, account.PasswordHash)
            ? new AuthenticatedAccount(
                account.UserAccountId,
                account.Username,
                account.Role,
                account.Employee?.FullName,
                account.Employee?.EmployeeCode,
                account.Employee?.EmploymentType,
                account.Employee?.Position,
                account.Employee?.Level)
            : null;
    }
}
