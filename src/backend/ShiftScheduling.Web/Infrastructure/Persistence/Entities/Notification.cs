using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Models.Entities;

// Entity lưu thông báo nội bộ gửi đến một tài khoản trong cơ sở dữ liệu.
public class Notification
{
    public int NotificationId { get; set; }
    public int ReceiverAccountId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public int? RelatedObjectId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }

    public UserAccount ReceiverAccount { get; set; } = null!;
}
