using ShiftScheduling.Features.Authentication.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.Authentication;

public sealed class AuthenticationServiceTests
{
    [Fact(DisplayName = "TC01-01 - UC01 - Đăng nhập bằng tài khoản hợp lệ")]
    public async Task AuthenticateAsync_WhenPasswordIsCorrect_ReturnsAccount()
    {
        // Sap xep
        var repository = new FakeAuthenticationRepository();
        repository.Accounts.Add(new UserAccount
        {
            UserAccountId = 1,
            Username = "admin",
            PasswordHash = "hashed:Demo@123",
            Role = UserRole.Admin
        });
        var service = new AuthenticationService(repository, new FakePasswordHashService());

        // Thuc hien
        var result = await service.AuthenticateAsync("ADMIN", "Demo@123");

        // Kiem tra
        Assert.NotNull(result);
        Assert.Equal(1, result!.UserAccountId);
    }

    [Fact(DisplayName = "TC01-02 - UC01 BR01 - Tài khoản bị khóa không được đăng nhập")]
    public async Task AuthenticateAsync_WhenAccountIsLocked_ReturnsNull()
    {
        // Sap xep
        var repository = new FakeAuthenticationRepository();
        repository.Accounts.Add(new UserAccount
        {
            UserAccountId = 1,
            Username = "admin",
            PasswordHash = "hashed:Demo@123",
            Role = UserRole.Admin,
            IsLocked = true
        });
        var service = new AuthenticationService(repository, new FakePasswordHashService());

        // Thuc hien
        var result = await service.AuthenticateAsync("admin", "Demo@123");

        // Kiem tra
        Assert.Null(result);
    }
}
