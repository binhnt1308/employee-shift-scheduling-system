using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.Accounts.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.ViewModels;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.Accounts;

public sealed class AccountServiceTests
{
    [Fact(DisplayName = "TC01-03 - UC01 BR01 - Tạo tài khoản trùng tên đăng nhập")]
    public async Task CreateAsync_WhenUsernameExists_ReturnsDuplicateError()
    {
        // Sap xep
        var repository = new FakeAccountRepository { UsernameExists = true };
        var service = CreateService(repository);
        var model = new AccountCreateViewModel
        {
            Username = "admin",
            Password = "Demo@123",
            ConfirmPassword = "Demo@123",
            Role = UserRole.Manager
        };

        // Thuc hien
        var result = await service.CreateAsync(model, 1);

        // Kiem tra
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.DuplicateUsername, result.ErrorCode);
    }

    [Fact(DisplayName = "TC01-04 - UC01 BR01 - Khóa tài khoản của chính mình")]
    public async Task SetLockedAsync_WhenTargetIsCurrentAccount_ReturnsSelfChangeError()
    {
        // Sap xep
        var service = CreateService(new FakeAccountRepository());

        // Thuc hien
        var result = await service.SetLockedAsync(1, true, 1);

        // Kiem tra
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.SelfAccountChange, result.ErrorCode);
    }

    [Fact(DisplayName = "TC01-05 - UC01 BR01 - Tạo tài khoản Employee không liên kết hồ sơ")]
    public async Task CreateAsync_WhenEmployeeRoleHasNoEmployee_ReturnsLinkError()
    {
        // Sap xep
        var repository = new FakeAccountRepository();
        var service = CreateService(repository);
        var model = new AccountCreateViewModel
        {
            Username = "nhanvien01",
            Password = "Demo@123",
            ConfirmPassword = "Demo@123",
            Role = UserRole.Employee
        };

        // Thuc hien
        var result = await service.CreateAsync(model, 1);

        // Kiem tra
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidEmployeeLink, result.ErrorCode);
    }

    [Fact(DisplayName = "TC01-06 - UC01 BR01 - Tạo tài khoản Employee liên kết hồ sơ hợp lệ")]
    public async Task CreateAsync_WhenEmployeeLinkIsValid_CreatesAccount()
    {
        // Sap xep
        var repository = new FakeAccountRepository();
        repository.Employees.Add(new Employee
        {
            EmployeeId = 10,
            EmployeeCode = "NV010",
            FullName = "Nhân viên mười",
            IsActive = true
        });
        var service = CreateService(repository);
        var model = new AccountCreateViewModel
        {
            Username = "nhanvien10",
            Password = "Demo@123",
            ConfirmPassword = "Demo@123",
            Role = UserRole.Employee,
            EmployeeId = 10
        };

        // Thuc hien
        var result = await service.CreateAsync(model, 1);

        // Kiem tra
        Assert.True(result.Succeeded);
        Assert.Single(repository.Accounts);
        Assert.Same(repository.Accounts[0], repository.Employees[0].UserAccount);
    }

    [Fact(DisplayName = "TC01-07 - UC01 BR01 - Khóa tài khoản Admin cuối cùng")]
    public async Task SetLockedAsync_WhenTargetIsLastAdmin_ReturnsLastAdminError()
    {
        // Sap xep
        var repository = new FakeAccountRepository { HasOtherActiveAdmin = false };
        repository.Accounts.Add(new UserAccount
        {
            UserAccountId = 2,
            Username = "admin2",
            Role = UserRole.Admin
        });
        var service = CreateService(repository);

        // Thuc hien
        var result = await service.SetLockedAsync(2, true, 1);

        // Kiem tra
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.LastActiveAdmin, result.ErrorCode);
    }

    private static AccountService CreateService(FakeAccountRepository repository)
    {
        return new AccountService(
            repository,
            new FakeAuditLogRepository(),
            new FakePasswordHashService(),
            new FakeUnitOfWork(),
            new FakeClock(),
            NullLogger<AccountService>.Instance);
    }
}
