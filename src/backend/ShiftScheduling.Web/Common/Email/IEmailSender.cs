namespace ShiftScheduling.Common.Email;

// Giao diện gửi email giúp nghiệp vụ không phụ thuộc trực tiếp vào SMTP và dễ thay bằng bản giả khi kiểm thử.
public interface IEmailSender
{
    Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default);
}
