namespace ShiftScheduling.Features.SchedulePeriods.ViewModels;

// Chứa các ViewModel dùng để trao đổi dữ liệu giữa Controller và View của chức năng kỳ lịch và định mức nhân sự.
public sealed class StaffingRequirementEditViewModel
{
    public int SchedulePeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public List<StaffingRequirementItemViewModel> Requirements { get; set; } = new();
}
