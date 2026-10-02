using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.SchedulePeriods.ViewModels;

// Chứa các ViewModel dùng để trao đổi dữ liệu giữa Controller và View của chức năng kỳ lịch và định mức nhân sự.
public sealed class SchedulePeriodListItemViewModel
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
    public int TotalRequirementsCount { get; set; }
    public bool IsDeadlineExpired { get; set; }
}
