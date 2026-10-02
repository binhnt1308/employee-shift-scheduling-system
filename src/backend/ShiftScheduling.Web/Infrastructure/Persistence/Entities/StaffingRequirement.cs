using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Models.Entities;

// Entity lưu định mức nhân sự cho một ngày và ca trong kỳ lịch trong cơ sở dữ liệu.
public class StaffingRequirement
{
    public int StaffingRequirementId { get; set; }
    public int SchedulePeriodId { get; set; }
    public DateOnly WorkDate { get; set; }
    public int ShiftTypeId { get; set; }
    public int RequiredSalesStaff { get; set; }
    public int RequiredCashiers { get; set; }
    public EmployeeLevel MinimumLevel { get; set; }
    public int RequiredExperiencedStaff { get; set; }

    public SchedulePeriod SchedulePeriod { get; set; } = null!;
    public ShiftType ShiftType { get; set; } = null!;
}
