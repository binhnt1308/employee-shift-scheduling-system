using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ShiftScheduling.Common.Email;

/// <summary>
/// Gửi email qua SMTP theo cấu hình. Lỗi cấu hình hoặc lỗi gửi chỉ được ghi log,
/// không làm hỏng nghiệp vụ chính đã hoàn tất trong hệ thống.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IOptions<SmtpSettings> options,
        ILogger<SmtpEmailSender> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        // Kiểm tra các điều kiện có thể xác định trước để không tạo kết nối SMTP vô ích.
        if (!_settings.Enabled)
        {
            _logger.LogDebug("SMTP is disabled. Skipping email to {ToEmail}.", toEmail);
            return;
        }

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogInformation("Recipient email is empty. Skipping email dispatch.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.Host) || string.IsNullOrWhiteSpace(_settings.SenderEmail))
        {
            _logger.LogWarning("SMTP Host or SenderEmail is not configured. Skipping email to {ToEmail}.", toEmail);
            return;
        }

        // MailAddress tự kiểm tra định dạng địa chỉ; địa chỉ sai chỉ làm bỏ qua email hiện tại.
        MailAddress fromAddress;
        MailAddress toAddress;
        try
        {
            fromAddress = new MailAddress(_settings.SenderEmail, _settings.SenderName);
            toAddress = new MailAddress(toEmail.Trim());
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Invalid email address format for sender '{Sender}' or recipient '{Recipient}'. Skipping email.", _settings.SenderEmail, toEmail);
            return;
        }

        using var message = new MailMessage(fromAddress, toAddress)
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_settings.UserName) && !string.IsNullOrWhiteSpace(_settings.Password))
        {
            client.Credentials = new NetworkCredential(_settings.UserName, _settings.Password);
        }

        // Email là kênh thông báo bổ sung, vì vậy lỗi SMTP được ghi log thay vì ném ngược ra luồng nghiệp vụ.
        try
        {
            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Email sent successfully to {ToEmail} with subject '{Subject}'.", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send email to {ToEmail} with subject '{Subject}'.", toEmail, subject);
        }
    }
}
