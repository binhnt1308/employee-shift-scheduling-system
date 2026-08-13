using ShiftScheduling.Common.Security;

namespace ShiftScheduling.Tests.Common;

public sealed class Pbkdf2PasswordHashServiceTests
{
    [Fact(DisplayName = "TC01-08 - UC01 - Hash và verify mật khẩu PBKDF2")]
    public void HashPassword_WhenCalled_DoesNotStorePlainTextAndCanVerify()
    {
        var service = new Pbkdf2PasswordHashService();
        const string password = "Demo@123";

        var passwordHash = service.HashPassword(password);

        Assert.DoesNotContain(password, passwordHash);
        Assert.True(service.VerifyPassword(password, passwordHash));
        Assert.False(service.VerifyPassword("SaiMatKhau", passwordHash));
    }
}
