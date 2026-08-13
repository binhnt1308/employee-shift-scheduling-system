using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Models.Entities;

public class WorkSchedule
{
    public int WorkScheduleId { get; set; }
    public int SchedulePeriodId { get; set; }
    public string ScheduleName { get; set; } = string.Empty;
    public ScheduleStatus Status { get; set; }
    public bool IsGeneratedByORTools { get; set; }
    public int CreatedByAccountId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? PublishedByAccountId { get; set; }
    public DateTime? PublishedAt { get; set; }

    public SchedulePeriod SchedulePeriod { get; set; } = null!;
    public UserAccount CreatedByAccount { get; set; } = null!;
    public UserAccount? PublishedByAccount { get; set; }
    public ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();
}
