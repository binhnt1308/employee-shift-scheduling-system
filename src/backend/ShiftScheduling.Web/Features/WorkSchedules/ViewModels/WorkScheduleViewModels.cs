using System.ComponentModel.DataAnnotations;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.WorkSchedules.ViewModels;

public sealed class WorkScheduleIndexViewModel
{
    public List<WorkSchedulePeriodItemViewModel> Periods { get; set; } = new();
}

public sealed class WorkSchedulePeriodItemViewModel
{
    public int SchedulePeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public RegistrationStatus RegistrationStatus { get; set; }
    public bool IsRegistrationEffectivelyOpen { get; set; }
    public string EffectiveRegistrationStatusName { get; set; } = string.Empty;
    public string EffectiveRegistrationBadgeClass { get; set; } = string.Empty;
    public int? WorkScheduleId { get; set; }
    public ScheduleStatus? ScheduleStatus { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int TotalAssignmentsCount { get; set; }
    public bool CanSchedule { get; set; }
}

public sealed class WorkScheduleEditViewModel
{
    public int SchedulePeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public RegistrationStatus RegistrationStatus { get; set; }
    public int WorkScheduleId { get; set; }
    public ScheduleStatus Status { get; set; }
    public bool IsGeneratedByORTools { get; set; }
    public List<SchedulingDayGridViewModel> Days { get; set; } = new();
    public ScheduleValidationResultViewModel ValidationResult { get; set; } = new();
}

public sealed class SchedulingDayGridViewModel
{
    public DateOnly WorkDate { get; set; }
    public string DayOfWeekName { get; set; } = string.Empty;
    public List<SchedulingShiftCellViewModel> Shifts { get; set; } = new();
}

public sealed class SchedulingShiftCellViewModel
{
    public DateOnly WorkDate { get; set; }
    public int ShiftTypeId { get; set; }
    public string ShiftCode { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int RequiredSales { get; set; }
    public int RequiredCashier { get; set; }
    public int AssignedSales { get; set; }
    public int AssignedCashier { get; set; }
    public int MissingSales { get; set; }
    public int MissingCashier { get; set; }
    public bool HasLevel2Plus { get; set; }
    public bool IsSatisfied { get; set; }
    public List<AssignedEmployeeItemViewModel> AssignedEmployees { get; set; } = new();
}

public sealed class AssignedEmployeeItemViewModel
{
    public int ShiftAssignmentId { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public EmployeePosition Position { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public EmployeeLevel Level { get; set; }
    public AssignmentSource Source { get; set; }
    public bool IsPreferred { get; set; }
    public bool HasApprovedLeave { get; set; }
    public int? ApprovedLeaveRequestId { get; set; }
}

public sealed class EligibleEmployeeOptionViewModel
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public EmployeePosition Position { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public EmployeeLevel Level { get; set; }
    public string EmploymentTypeLabel => EmploymentType == EmploymentType.FullTime ? "FT" : "PT";
    public string PositionLabel => Position.ToDisplayName();
    public bool HasAvailability { get; set; }
    public bool HasApprovedLeave { get; set; }
    public bool HasOverlappingShift { get; set; }
    public bool IsEligible { get; set; }
    public string? IneligibilityReason { get; set; }
}

public sealed class ManualAssignRequestViewModel
{
    public int SchedulePeriodId { get; set; }
    public DateOnly WorkDate { get; set; }
    public int ShiftTypeId { get; set; }
    public int EmployeeId { get; set; }
    public int? ReplaceShiftAssignmentId { get; set; }
}

public sealed class ManualRemoveRequestViewModel
{
    public int SchedulePeriodId { get; set; }
    public int ShiftAssignmentId { get; set; }
}

public sealed class ScheduleValidationResultViewModel
{
    public bool IsValid { get; set; }
    public List<string> HardErrors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<EmployeeSummaryItemViewModel> EmployeeSummaries { get; set; } = new();
}

public sealed class EmployeeSummaryItemViewModel
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public EmployeePosition Position { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public EmployeeLevel Level { get; set; }
    public int TotalShifts { get; set; }
    public int TotalWorkDays { get; set; }
    public decimal TotalHours { get; set; }
    public string Warning { get; set; } = string.Empty;
}

public sealed class OrToolsProposalResultViewModel
{
    public bool Succeeded { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public double SolveTimeSeconds { get; set; }
    public List<ProposedAssignmentItemViewModel> ProposedAssignments { get; set; } = new();
    public ScheduleValidationResultViewModel ValidationResult { get; set; } = new();
}

public sealed class ProposedAssignmentItemViewModel
{
    public DateOnly WorkDate { get; set; }
    public int ShiftTypeId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public EmployeePosition Position { get; set; }
    public EmploymentType EmploymentType { get; set; }

    public string PositionLabel => Position.ToDisplayName();
}

public sealed class LeaveReplacementViewModel
{
    public int LeaveRequestId { get; set; }
    public int SchedulePeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeFullName { get; set; } = string.Empty;
    public EmployeePosition EmployeePosition { get; set; }
    public DateOnly LeaveDate { get; set; }
    public int? ShiftTypeId { get; set; }
    public string? ShiftTypeName { get; set; }
    public string LeaveReason { get; set; } = string.Empty;
    public LeaveRequestStatus LeaveStatus { get; set; }
    public bool IsPendingLeave => LeaveStatus == LeaveRequestStatus.Pending;
    public string RowVersion { get; set; } = string.Empty;
    public string? ManagerNote { get; set; }
    public List<LeaveReplacementAssignmentViewModel> AffectedAssignments { get; set; } = new();
}

public sealed class LeaveReplacementAssignmentViewModel
{
    public int ShiftAssignmentId { get; set; }
    public int WorkScheduleId { get; set; }
    public DateOnly WorkDate { get; set; }
    public int ShiftTypeId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public EmployeePosition RequiredPosition { get; set; }
    public bool IsEnded { get; set; }
    public List<LeaveReplacementCandidateViewModel> Candidates { get; set; } = new();
}

public sealed class LeaveReplacementCandidateViewModel
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public EmployeePosition Position { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public EmployeeLevel Level { get; set; }
    public AvailabilityPreferenceLevel PreferenceLevel { get; set; }
}

public sealed class LeaveReplacementRequestViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Mã đơn nghỉ không hợp lệ.")]
    public int LeaveRequestId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Mã ca phân công không hợp lệ.")]
    public int ShiftAssignmentId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Mã nhân viên thay thế không hợp lệ.")]
    public int CandidateEmployeeId { get; set; }
}

public sealed class ApproveLeaveWithReplacementRequestViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Mã đơn nghỉ không hợp lệ.")]
    public int LeaveRequestId { get; set; }

    public string RowVersion { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
    public string? ManagerNote { get; set; }

    public List<ShiftReplacementItemViewModel> Replacements { get; set; } = new();
}

public sealed class ShiftReplacementItemViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Mã ca phân công không hợp lệ.")]
    public int ShiftAssignmentId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Mã nhân viên thay thế không hợp lệ.")]
    public int CandidateEmployeeId { get; set; }
}

public sealed class ApproveLeaveReplacementResult
{
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public List<ReplacedShiftSummary> ReplacedShifts { get; set; } = new();
}

public sealed class ReplacedShiftSummary
{
    public DateOnly WorkDate { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public string ReplacementEmployeeName { get; set; } = string.Empty;
    public string ReplacementEmployeeCode { get; set; } = string.Empty;
}

public sealed class ApproveEmergencyLeaveRequestViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Mã đơn nghỉ không hợp lệ.")]
    public int LeaveRequestId { get; set; }

    public string RowVersion { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
    public string? ManagerNote { get; set; }
}
