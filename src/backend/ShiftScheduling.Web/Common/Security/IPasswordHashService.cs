namespace ShiftScheduling.Common.Security;

public interface IPasswordHashService
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string encodedHash);
}
