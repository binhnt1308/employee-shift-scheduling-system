using System.ComponentModel.DataAnnotations;

namespace ShiftScheduling.Features.SchedulePeriods.ViewModels;

public sealed class StaffingRequirementItemViewModel
{
    public int StaffingRequirementId { get; set; }
    public DateOnly WorkDate { get; set; }
    public int ShiftTypeId { get; set; }
    public string ShiftCode { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    [Range(0, 50, ErrorMessage = "Số nhân viên bán hàng phải từ 0 trở lên.")]
    [Display(Name = "Bán hàng")]
    public int RequiredSalesStaff { get; set; }

    [Range(0, 50, ErrorMessage = "Số nhân viên thu ngân phải từ 0 trở lên.")]
    [Display(Name = "Thu ngân")]
    public int RequiredCashiers { get; set; }
}
