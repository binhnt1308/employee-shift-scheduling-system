using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Models.Entities;

public class Availability
{
    public int AvailabilityId { get; set; }
    public int EmployeeId { get; set; }
    public int SchedulePeriodId { get; set; }
    public DateOnly WorkDate { get; set; }
    public int ShiftTypeId { get; set; }

    // NC-02: Mức nguyện vọng của ca rảnh. Mặc định Normal cho dữ liệu cũ.
    public AvailabilityPreferenceLevel PreferenceLevel { get; set; } = AvailabilityPreferenceLevel.Normal;

    public DateTime RegisteredAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Employee Employee { get; set; } = null!;
    public SchedulePeriod SchedulePeriod { get; set; } = null!;
    public ShiftType ShiftType { get; set; } = null!;
}
