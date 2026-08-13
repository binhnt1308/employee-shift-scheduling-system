using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.SchedulePeriods.ViewModels;

public sealed class SchedulePeriodListItemViewModel
{
    public int SchedulePeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public DateTime AvailabilityDeadline { get; set; }
    public RegistrationStatus RegistrationStatus { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public int TotalRequirementsCount { get; set; }
    public bool IsDeadlineExpired { get; set; }
}
