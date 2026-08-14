using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Models.Entities;

public class LeaveRequest
{
    public int LeaveRequestId { get; set; }
    public int EmployeeId { get; set; }
    public int SchedulePeriodId { get; set; }
    public DateOnly LeaveDate { get; set; }
    public int? ShiftTypeId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public LeaveRequestStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public int? ApprovedByAccountId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ManagerNote { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Employee Employee { get; set; } = null!;
    public SchedulePeriod SchedulePeriod { get; set; } = null!;
    public ShiftType? ShiftType { get; set; }
    public UserAccount? ApprovedByAccount { get; set; }
}
