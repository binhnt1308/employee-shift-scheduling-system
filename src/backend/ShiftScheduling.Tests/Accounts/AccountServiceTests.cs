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
        var repository = new FakeAccountRepository { UsernameExists = true };
        var service = CreateService(repository);
        var model = new AccountCreateViewModel
        {
            Username = "admin",
            Password = "Demo@123",
            ConfirmPassword = "Demo@123",
            Role = UserRole.Manager
        };

        var result = await service.CreateAsync(model, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.DuplicateUsername, result.ErrorCode);
    }

    [Fact(DisplayName = "TC01-04 - UC01 BR01 - Khóa tài khoản của chính mình")]
    public async Task SetLockedAsync_WhenTargetIsCurrentAccount_ReturnsSelfChangeError()
    {
        var service = CreateService(new FakeAccountRepository());

        var result = await service.SetLockedAsync(1, true, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.SelfAccountChange, result.ErrorCode);
    }

    [Fact(DisplayName = "TC01-05 - UC01 BR01 - Tạo tài khoản Employee không liên kết hồ sơ")]
    public async Task CreateAsync_WhenEmployeeRoleHasNoEmployee_ReturnsLinkError()
    {
        var repository = new FakeAccountRepository();
        var service = CreateService(repository);
        var model = new AccountCreateViewModel
        {
            Username = "nhanvien01",
            Password = "Demo@123",
            ConfirmPassword = "Demo@123",
            Role = UserRole.Employee
        };

        var result = await service.CreateAsync(model, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidEmployeeLink, result.ErrorCode);
    }

    [Fact(DisplayName = "TC01-06 - UC01 BR01 - Tạo tài khoản Employee liên kết hồ sơ hợp lệ")]
    public async Task CreateAsync_WhenEmployeeLinkIsValid_CreatesAccount()
    {
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

        var result = await service.CreateAsync(model, 1);

        Assert.True(result.Succeeded);
        Assert.Single(repository.Accounts);
        Assert.Same(repository.Accounts[0], repository.Employees[0].UserAccount);
    }

    [Fact(DisplayName = "TC01-07 - UC01 BR01 - Khóa tài khoản Admin cuối cùng")]
    public async Task SetLockedAsync_WhenTargetIsLastAdmin_ReturnsLastAdminError()
    {
        var repository = new FakeAccountRepository { HasOtherActiveAdmin = false };
        repository.Accounts.Add(new UserAccount
        {
            UserAccountId = 2,
            Username = "admin2",
            Role = UserRole.Admin
        });
        var service = CreateService(repository);

        var result = await service.SetLockedAsync(2, true, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.LastActiveAdmin, result.ErrorCode);
    }

    [Fact(DisplayName = "TC01-09 - UC01 BR01 - Khóa tài khoản bình thường")]
    public async Task SetLockedAsync_WhenTargetIsNormalAccount_LocksAccount()
    {
        var repository = new FakeAccountRepository();
        var targetAccount = new UserAccount
        {
            UserAccountId = 2,
            Username = "emp1",
            Role = UserRole.Employee,
            IsLocked = false
        };
        repository.Accounts.Add(targetAccount);
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(repository, unitOfWork);

        var result = await service.SetLockedAsync(2, true, 1, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(targetAccount.IsLocked);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact(DisplayName = "TC01-10 - UC01 BR01 - Mở khóa tài khoản bị khóa")]
    public async Task SetLockedAsync_WhenTargetIsLockedAccount_UnlocksAccount()
    {
        var repository = new FakeAccountRepository();
        var targetAccount = new UserAccount
        {
            UserAccountId = 2,
            Username = "emp1",
            Role = UserRole.Employee,
            IsLocked = true
        };
        repository.Accounts.Add(targetAccount);
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(repository, unitOfWork);

        var result = await service.SetLockedAsync(2, false, 1, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.False(targetAccount.IsLocked);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private static AccountService CreateService(
        FakeAccountRepository repository,
        FakeUnitOfWork? unitOfWork = null)
    {
        return new AccountService(
            repository,
            new FakeAuditLogRepository(),
            new FakePasswordHashService(),
            unitOfWork ?? new FakeUnitOfWork(),
            new FakeClock(),
            NullLogger<AccountService>.Instance);
    }
}
