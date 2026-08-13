using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Models.Entities;

public class SchedulePeriod
{
    public int SchedulePeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public DateTime AvailabilityDeadline { get; set; }
    public RegistrationStatus RegistrationStatus { get; set; }
    public int CreatedByAccountId { get; set; }
    public DateTime CreatedAt { get; set; }

    public UserAccount CreatedByAccount { get; set; } = null!;
    public ICollection<StaffingRequirement> StaffingRequirements { get; set; } = new List<StaffingRequirement>();
    public ICollection<Availability> Availabilities { get; set; } = new List<Availability>();
    public ICollection<WorkSchedule> WorkSchedules { get; set; } = new List<WorkSchedule>();
    public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
}
