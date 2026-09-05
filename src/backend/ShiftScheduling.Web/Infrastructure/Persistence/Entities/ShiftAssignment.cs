using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Models.Entities;

public class ShiftAssignment
{
    public int ShiftAssignmentId { get; set; }
    public int WorkScheduleId { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }
    public int ShiftTypeId { get; set; }
    public AssignmentSource Source { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public decimal? ActualWorkedHours { get; set; }
    public string? WorkdayAdjustmentReason { get; set; }

    public WorkSchedule WorkSchedule { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
    public ShiftType ShiftType { get; set; } = null!;
    public ICollection<ShiftTransferRequest> ShiftTransferRequests { get; set; } = new List<ShiftTransferRequest>();
}
