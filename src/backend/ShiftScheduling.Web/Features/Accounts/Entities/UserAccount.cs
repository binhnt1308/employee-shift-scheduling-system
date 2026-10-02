using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Models.Entities;

// Entity lưu tài khoản đăng nhập, vai trò và liên kết với hồ sơ nhân viên.
public class UserAccount
{
    public int UserAccountId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CreatedByAccountId { get; set; }

    public UserAccount? CreatedByAccount { get; set; }
    public ICollection<UserAccount> CreatedAccounts { get; set; } = new List<UserAccount>();
    public Employee? Employee { get; set; }
    public ICollection<WorkSchedule> CreatedWorkSchedules { get; set; } = new List<WorkSchedule>();
    public ICollection<WorkSchedule> PublishedWorkSchedules { get; set; } = new List<WorkSchedule>();
    public ICollection<LeaveRequest> ReviewedLeaveRequests { get; set; } = new List<LeaveRequest>();
    public ICollection<ShiftTransferRequest> ReviewedShiftTransferRequests { get; set; }
        = new List<ShiftTransferRequest>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
