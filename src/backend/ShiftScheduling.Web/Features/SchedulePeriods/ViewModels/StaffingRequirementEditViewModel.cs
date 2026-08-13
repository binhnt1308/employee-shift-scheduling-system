namespace ShiftScheduling.Features.SchedulePeriods.ViewModels;

public sealed class StaffingRequirementEditViewModel
{
    public int SchedulePeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public List<StaffingRequirementItemViewModel> Requirements { get; set; } = new();
}
