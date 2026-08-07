using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Models.Entities;

public class ShiftTransferRequest
{
    public int ShiftTransferRequestId { get; set; }
    public int ShiftAssignmentId { get; set; }
    public int SenderEmployeeId { get; set; }
    public int ReceiverEmployeeId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ReceiverStatus ReceiverStatus { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReceiverRespondedAt { get; set; }
    public int? ApprovedByAccountId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ManagerNote { get; set; }

    public ShiftAssignment ShiftAssignment { get; set; } = null!;
    public Employee SenderEmployee { get; set; } = null!;
    public Employee ReceiverEmployee { get; set; } = null!;
    public UserAccount? ApprovedByAccount { get; set; }
}
