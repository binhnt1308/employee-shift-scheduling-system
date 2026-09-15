using System.ComponentModel.DataAnnotations;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.LeaveRequests.ViewModels;

public sealed class LeaveRequestIndexViewModel
{
    public bool IsManager { get; init; }
    public int? SchedulePeriodId { get; init; }
    public LeaveRequestStatus? Status { get; init; }
    public IReadOnlyList<LeavePeriodOptionViewModel> Periods { get; init; } = [];
    public PagedResult<LeaveRequestListItemViewModel> Requests { get; init; } = new([], 1, 10, 0);
}

public sealed class LeaveRequestListItemViewModel
{
    public int LeaveRequestId { get; init; }
    public string EmployeeCode { get; init; } = string.Empty;
    public string EmployeeName { get; init; } = string.Empty;
    public string PeriodName { get; init; } = string.Empty;
    public DateOnly LeaveDate { get; init; }
    public string ShiftName { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public LeaveRequestStatus Status { get; init; }
    public DateTime SubmittedAt { get; init; }
    public bool HasPublishedAssignment { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class LeaveRequestCreateViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn kỳ lịch.")]
    [Display(Name = "Kỳ lịch")]
    public int SchedulePeriodId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày xin nghỉ.")]
    [Display(Name = "Ngày xin nghỉ")]
    public DateOnly LeaveDate { get; set; }

    [Display(Name = "Ca xin nghỉ")]
    public int? ShiftTypeId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do xin nghỉ.")]
    [StringLength(1000, MinimumLength = 3, ErrorMessage = "Lý do phải từ 3 đến 1000 ký tự.")]
    [Display(Name = "Lý do")]
    public string Reason { get; set; } = string.Empty;

    public IReadOnlyList<LeavePeriodOptionViewModel> Periods { get; set; } = [];
    public IReadOnlyList<LeaveShiftOptionViewModel> ShiftTypes { get; set; } = [];
    public IReadOnlyList<DateOnly> AvailableDates { get; set; } = [];
    public IReadOnlyList<LeaveAssignmentOptionViewModel> AvailableAssignments { get; set; } = [];
}

public sealed class LeaveRequestDetailsViewModel
{
    public int LeaveRequestId { get; init; }
    public string EmployeeCode { get; init; } = string.Empty;
    public string EmployeeName { get; init; } = string.Empty;
    public string PeriodName { get; init; } = string.Empty;
    public DateOnly LeaveDate { get; init; }
    public string ShiftName { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public LeaveRequestStatus Status { get; init; }
    public DateTime SubmittedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? ManagerNote { get; init; }
    public bool HasPublishedAssignment { get; init; }
    public bool HasDraftAssignment { get; init; }
    public bool CanReview { get; init; }
    public bool CanCancel { get; init; }
    public bool IsManager { get; init; }
    public int SchedulePeriodId { get; init; }
    public bool NeedsReplacement => IsManager && Status == LeaveRequestStatus.Approved && HasPublishedAssignment;
    public bool ReplacementHandled => IsManager && Status == LeaveRequestStatus.Approved && !HasPublishedAssignment;
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class LeaveRequestReviewViewModel
{
    public int LeaveRequestId { get; set; }
    public string RowVersion { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Ghi chú không được vượt quá 1000 ký tự.")]
    [Display(Name = "Ghi chú của Quản lý")]
    public string? ManagerNote { get; set; }

    public LeaveRequestDetailsViewModel Details { get; set; } = new();

    public ShiftScheduling.Features.WorkSchedules.ViewModels.LeaveReplacementViewModel? ReplacementPreview { get; set; }
}

public sealed record LeavePeriodOptionViewModel(
    int SchedulePeriodId,
    string PeriodName,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate);

public sealed record LeaveShiftOptionViewModel(
    int ShiftTypeId,
    string ShiftName,
    TimeOnly StartTime,
    TimeOnly EndTime);

public sealed record LeaveAssignmentOptionViewModel(
    int SchedulePeriodId,
    DateOnly WorkDate,
    int ShiftTypeId,
    string ShiftName,
    TimeOnly StartTime,
    TimeOnly EndTime);
