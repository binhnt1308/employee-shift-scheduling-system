namespace ShiftScheduling.Models.Entities;

// Entity biểu diễn cấu hình một loại ca làm việc và phiên bản cấu hình trong miền nghiệp vụ cấu hình ca làm việc.
public class ShiftType
{
    public int ShiftTypeId { get; set; }
    public string ShiftCode { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public decimal DurationHours { get; set; }
    public int DisplayOrder { get; set; }
    public int ConfigurationVersion { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public int MaxShiftsPerEmployeePerDay { get; set; } = 1;
    public bool AllowSplitShift { get; set; } = false;

    public ICollection<StaffingRequirement> StaffingRequirements { get; set; } = new List<StaffingRequirement>();
    public ICollection<Availability> Availabilities { get; set; } = new List<Availability>();
    public ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();
    public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
}
