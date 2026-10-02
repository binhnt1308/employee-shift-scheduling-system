namespace ShiftScheduling.Common.Email;

// Nhóm cấu hình SMTP được bind từ appsettings hoặc biến môi trường.
public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "Shift Scheduling System";
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
