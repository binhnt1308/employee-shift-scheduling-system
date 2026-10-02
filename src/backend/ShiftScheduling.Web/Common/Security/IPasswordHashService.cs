namespace ShiftScheduling.Common.Security;

// Giao diện băm và xác thực mật khẩu; mã nguồn nghiệp vụ không phụ thuộc thuật toán cụ thể.
public interface IPasswordHashService
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string encodedHash);
}
