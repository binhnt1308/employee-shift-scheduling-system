using ShiftScheduling.Common.Security;
using ShiftScheduling.Infrastructure.Persistence.Seed;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.Seed;

public sealed class DemoDataSeederAccountTests
{
    [Fact(DisplayName = "Demo seeder đồng bộ mật khẩu chỉ cho tài khoản demo đã tồn tại")]
    public void SynchronizeExistingDemoAccountPasswords_UpdatesOnlyDemoAccounts()
    {
        IPasswordHashService passwordHashService = new FakePasswordHashService();

        var admin = CreateAccount("admin", "admin123", UserRole.Admin, passwordHashService);
        var manager = CreateAccount("quanly", "quanly123", UserRole.Manager, passwordHashService);
        var employee = CreateAccount("nv01", "oldEmployee123", UserRole.Employee, passwordHashService);
        var nonDemo = CreateAccount("other-user", "KeepMe123!", UserRole.Employee, passwordHashService);
        var originalNonDemoHash = nonDemo.PasswordHash;

        Assert.False(passwordHashService.VerifyPassword(
            DemoDataSeeder.DefaultDemoPassword, admin.PasswordHash));
        Assert.False(passwordHashService.VerifyPassword(
            DemoDataSeeder.DefaultDemoPassword, manager.PasswordHash));
        Assert.False(passwordHashService.VerifyPassword(
            DemoDataSeeder.DefaultDemoPassword, employee.PasswordHash));

        DemoDataSeeder.SynchronizeExistingDemoAccountPasswords(
            [admin, manager, employee, nonDemo],
            passwordHashService);

        Assert.True(passwordHashService.VerifyPassword(
            DemoDataSeeder.DefaultDemoPassword, admin.PasswordHash));
        Assert.True(passwordHashService.VerifyPassword(
            DemoDataSeeder.DefaultDemoPassword, manager.PasswordHash));
        Assert.True(passwordHashService.VerifyPassword(
            DemoDataSeeder.DefaultDemoPassword, employee.PasswordHash));
        Assert.Equal(originalNonDemoHash, nonDemo.PasswordHash);
    }

    private static UserAccount CreateAccount(
        string username,
        string password,
        UserRole role,
        IPasswordHashService passwordHashService)
    {
        return new UserAccount
        {
            Username = username,
            PasswordHash = passwordHashService.HashPassword(password),
            Role = role
        };
    }
}
