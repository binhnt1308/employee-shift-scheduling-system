using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.SchedulePeriods.ViewModels;

public sealed class SchedulePeriodDetailsViewModel
{
    public int SchedulePeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public DateTime AvailabilityDeadline { get; set; }
    public RegistrationStatus RegistrationStatus { get; set; }
    public string EffectiveStatusName { get; set; } = string.Empty;
    public bool NeedsAttention { get; set; }
    public string DisplayBadgeClass { get; set; } = "badge-ended";
    public string CreatedByUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<StaffingRequirementItemViewModel> Requirements { get; set; } = new();
    public bool CanOpenRegistration { get; set; }
}
