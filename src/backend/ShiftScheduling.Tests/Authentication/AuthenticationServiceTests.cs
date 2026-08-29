using ShiftScheduling.Features.Authentication.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Tests.Fakes;
using Xunit;

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

    [Fact(DisplayName = "TC-AUTH-PROFILE-01 - Employee login: AuthenticatedAccount resolves employee identity properties")]
    public async Task AuthenticateAsync_WhenEmployeeLinked_ResolvesProfileProperties()
    {
        var repository = new FakeAuthenticationRepository();
        repository.Accounts.Add(new UserAccount
        {
            UserAccountId = 2,
            Username = "nv01",
            PasswordHash = "hashed:Demo@123",
            Role = UserRole.Employee,
            Employee = new Employee
            {
                EmployeeId = 1,
                EmployeeCode = "NV01",
                FullName = "Nguyễn Văn B",
                EmploymentType = EmploymentType.PartTime,
                Position = EmployeePosition.Sales,
                Level = EmployeeLevel.Level2
            }
        });
        var service = new AuthenticationService(repository, new FakePasswordHashService());

        var result = await service.AuthenticateAsync("nv01", "Demo@123");

        Assert.NotNull(result);
        Assert.Equal("Nguyễn Văn B", result!.FullName);
        Assert.Equal("NV01", result.EmployeeCode);
        Assert.Equal(EmploymentType.PartTime, result.EmploymentType);
        Assert.Equal(EmployeePosition.Sales, result.Position);
        Assert.Equal(EmployeeLevel.Level2, result.Level);
    }

    [Fact(DisplayName = "TC-AUTH-PROFILE-03 - Account without Employee: login succeeds without crash")]
    public async Task AuthenticateAsync_WhenNoEmployeeLinked_SucceedsWithNullEmployeeProperties()
    {
        var repository = new FakeAuthenticationRepository();
        repository.Accounts.Add(new UserAccount
        {
            UserAccountId = 10,
            Username = "manager1",
            PasswordHash = "hashed:Demo@123",
            Role = UserRole.Manager
        });
        var service = new AuthenticationService(repository, new FakePasswordHashService());

        var result = await service.AuthenticateAsync("manager1", "Demo@123");

        Assert.NotNull(result);
        Assert.Equal(UserRole.Manager, result!.Role);
        Assert.Null(result.FullName);
        Assert.Null(result.EmployeeCode);
        Assert.Null(result.EmploymentType);
    }

    [Fact(DisplayName = "TC-AUTH-PROFILE-04 - ClaimTypes.Role remains unchanged")]
    public async Task AuthenticateAsync_PreservesUserRole()
    {
        var repository = new FakeAuthenticationRepository();
        repository.Accounts.Add(new UserAccount
        {
            UserAccountId = 3,
            Username = "employee3",
            PasswordHash = "hashed:Demo@123",
            Role = UserRole.Employee,
            Employee = new Employee
            {
                EmployeeId = 3,
                EmployeeCode = "NV03",
                FullName = "Nguyễn Thanh Bình",
                EmploymentType = EmploymentType.FullTime,
                Position = EmployeePosition.Cashier,
                Level = EmployeeLevel.Level3
            }
        });
        var service = new AuthenticationService(repository, new FakePasswordHashService());

        var result = await service.AuthenticateAsync("employee3", "Demo@123");

        Assert.NotNull(result);
        Assert.Equal(UserRole.Employee, result!.Role);
    }
}
