using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Models.Entities;

public class Employee
{
    public int EmployeeId { get; set; }
    public int? UserAccountId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public EmployeePosition Position { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public EmployeeLevel Level { get; set; }
    public DateOnly StartDate { get; set; }
    public bool IsActive { get; set; } = true;

    public UserAccount? UserAccount { get; set; }
    public ICollection<Availability> Availabilities { get; set; } = new List<Availability>();
    public ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();
    public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
    public ICollection<ShiftTransferRequest> SentShiftTransferRequests { get; set; }
        = new List<ShiftTransferRequest>();
    public ICollection<ShiftTransferRequest> ReceivedShiftTransferRequests { get; set; }
        = new List<ShiftTransferRequest>();
}
