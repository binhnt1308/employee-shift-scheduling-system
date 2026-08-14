using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.WorkSchedules.Services;

public partial class WorkScheduleService
{
    /// <inheritdoc />
    public async Task<List<EligibleEmployeeOptionViewModel>> GetEligibleEmployeesForShiftAsync(
        int schedulePeriodId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        var period = await _periodRepository.GetByIdAsync(schedulePeriodId, true, false, cancellationToken);
        if (period is null) return new List<EligibleEmployeeOptionViewModel>();

        var activeEmployees = await _employeeRepository.GetAllActiveAsync(cancellationToken);
        var availabilities = await _availabilityRepository.GetByPeriodWithDetailsAsync(
            schedulePeriodId, null, null, workDate, shiftTypeId, cancellationToken);
        var leaves = await _leaveRequestRepository.GetApprovedLeavesByPeriodAsync(
            schedulePeriodId, cancellationToken);
        var schedule = await _workScheduleRepository.GetByPeriodIdAsync(
            schedulePeriodId, true, true, cancellationToken);
        var allShifts = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);

        var currentAssignments = schedule?.ShiftAssignments.ToList() ?? new List<ShiftAssignment>();
        var availSet = availabilities.Select(a => a.EmployeeId).ToHashSet();
        var leaveSet = leaves.Where(l => l.Status == LeaveRequestStatus.Approved).ToList();
        var shiftTypeMap = allShifts.ToDictionary(s => s.ShiftTypeId);

        var result = activeEmployees.Select(emp => BuildEligibleEmployeeOption(
            emp, workDate, shiftTypeId, period.WeekStartDate,
            availSet, leaveSet, currentAssignments, shiftTypeMap)).ToList();

        return result.OrderByDescending(e => e.IsEligible)
                     .ThenBy(e => e.Position)
                     .ThenBy(e => e.FullName)
                     .ToList();
    }

    private static EligibleEmployeeOptionViewModel BuildEligibleEmployeeOption(
        Employee emp,
        DateOnly workDate,
        int shiftTypeId,
        DateOnly weekStartDate,
        HashSet<int> availSet,
        List<LeaveRequest> leaveSet,
        List<ShiftAssignment> currentAssignments,
        Dictionary<int, ShiftType> shiftTypeMap)
    {
        var hasAvail = availSet.Contains(emp.EmployeeId);
        var hasLeave = leaveSet.Any(l => l.EmployeeId == emp.EmployeeId && l.LeaveDate == workDate
            && (!l.ShiftTypeId.HasValue || l.ShiftTypeId.Value == shiftTypeId));
        var hasOverlap = currentAssignments.Any(a => a.EmployeeId == emp.EmployeeId
            && a.WorkDate == workDate && a.ShiftTypeId == shiftTypeId);

        var reasons = new List<string>();
        if (!hasAvail) reasons.Add("Chưa đăng ký rảnh");
        if (hasLeave) reasons.Add("Có đơn nghỉ đã duyệt");
        if (hasOverlap) reasons.Add("Đã được phân vào ca này");

        var empAssignments = currentAssignments.Where(a => a.EmployeeId == emp.EmployeeId).ToList();
        empAssignments.Add(new ShiftAssignment
        {
            EmployeeId = emp.EmployeeId,
            WorkDate = workDate,
            ShiftTypeId = shiftTypeId
        });
        var restErrors = SchedulingRestRuleEvaluator.EvaluateEmployeeRestRules(
            emp, empAssignments, shiftTypeMap, weekStartDate);
        if (restErrors.Count > 0) reasons.Add("Vi phạm thời gian nghỉ tối thiểu (BR20/BR21)");

        var isEligible = hasAvail && !hasLeave && !hasOverlap && restErrors.Count == 0;
        return new EligibleEmployeeOptionViewModel
        {
            EmployeeId = emp.EmployeeId,
            EmployeeCode = emp.EmployeeCode,
            FullName = emp.FullName,
            Position = emp.Position,
            EmploymentType = emp.EmploymentType,
            Level = emp.Level,
            HasAvailability = hasAvail,
            HasApprovedLeave = hasLeave,
            HasOverlappingShift = hasOverlap,
            IsEligible = isEligible,
            IneligibilityReason = isEligible ? null : string.Join(", ", reasons)
        };
    }

    /// <inheritdoc />
    public async Task<ServiceResult> AssignEmployeeManualAsync(
        ManualAssignRequestViewModel model,
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var period = await _periodRepository.GetByIdAsync(model.SchedulePeriodId, true, true, cancellationToken);
        if (period is null)
            return ServiceResult.Failure(
                ErrorCodes.SchedulePeriodNotFound,
                "Không tìm thấy kỳ lịch.");

        var draftState = await EnsureValidDraftStateAsync(model.SchedulePeriodId, accountId, cancellationToken);
        if (!draftState.Succeeded) return draftState.Result!;
        var schedule = draftState.Schedule!;

        var empCheck = await ValidateEmployeeAvailabilityAndLeavesAsync(model, cancellationToken);
        if (!empCheck.Succeeded) return empCheck.Result!;
        var employee = empCheck.Employee!;

        var currentList = schedule.ShiftAssignments.ToList();
        if (model.ReplaceShiftAssignmentId.HasValue && model.ReplaceShiftAssignmentId.Value > 0)
            currentList.RemoveAll(a => a.ShiftAssignmentId == model.ReplaceShiftAssignmentId.Value);

        if (currentList.Any(a => a.EmployeeId == employee.EmployeeId
            && a.WorkDate == model.WorkDate && a.ShiftTypeId == model.ShiftTypeId))
        {
            return ServiceResult.Failure(
                ErrorCodes.ShiftAssignmentOverlap,
                $"Nhân viên {employee.FullName} đã được phân vào ca này.");
        }

        var candidateList = currentList.Select(a => (a.EmployeeId, a.WorkDate, a.ShiftTypeId, a.Source)).ToList();
        candidateList.Add((employee.EmployeeId, model.WorkDate, model.ShiftTypeId, AssignmentSource.Manual));

        var allShifts = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);
        var ruleCheck = ValidateCandidateRules(period, employee, candidateList, allShifts);
        if (!ruleCheck.Succeeded) return ruleCheck.Result!;

        return await SaveManualAssignmentAsync(schedule, employee, model, candidateList, accountId, cancellationToken);
    }

    private async Task<(bool Succeeded, WorkSchedule? Schedule, ServiceResult? Result)> EnsureValidDraftStateAsync(
        int schedulePeriodId,
        int accountId,
        CancellationToken cancellationToken)
    {
        await _workScheduleRepository.EnsureDraftExistsAsync(
            schedulePeriodId, accountId, _clock.UtcNow, cancellationToken);
        var schedule = await _workScheduleRepository.GetByPeriodIdAsync(
            schedulePeriodId, false, true, cancellationToken);
        if (schedule is null || schedule.Status == ScheduleStatus.Published)
        {
            return (false, null, ServiceResult.Failure(
                ErrorCodes.InvalidScheduleStatus, "Lịch đã công bố không thể chỉnh sửa."));
        }

        return (true, schedule, null);
    }

    private async Task<(bool Succeeded, Employee? Employee, ServiceResult? Result)>
        ValidateEmployeeAvailabilityAndLeavesAsync(
        ManualAssignRequestViewModel model,
        CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(model.EmployeeId, true, cancellationToken);
        if (employee is null || !employee.IsActive)
        {
            return (false, null, ServiceResult.Failure(
                ErrorCodes.EmployeeNotFound, "Nhân viên không tồn tại hoặc đã ngưng hoạt động."));
        }

        var avails = await _availabilityRepository.GetByEmployeeAndPeriodAsync(
            employee.EmployeeId, model.SchedulePeriodId, cancellationToken);
        if (!avails.Any(a => a.WorkDate == model.WorkDate && a.ShiftTypeId == model.ShiftTypeId))
        {
            return (false, null, ServiceResult.Failure(
                ErrorCodes.EmployeeNotAvailable,
                $"Nhân viên {employee.FullName} chưa đăng ký rảnh cho ca này."));
        }

        var leaves = await _leaveRequestRepository.GetApprovedLeavesByPeriodAsync(
            model.SchedulePeriodId, cancellationToken);
        var hasLeave = leaves.Any(l => l.EmployeeId == employee.EmployeeId && l.LeaveDate == model.WorkDate
            && (!l.ShiftTypeId.HasValue || l.ShiftTypeId.Value == model.ShiftTypeId));
        if (hasLeave)
        {
            return (false, null, ServiceResult.Failure(
                ErrorCodes.EmployeeOnLeave,
                $"Nhân viên {employee.FullName} có đơn xin nghỉ đã được duyệt."));
        }

        return (true, employee, null);
    }

    private static (bool Succeeded, ServiceResult? Result) ValidateCandidateRules(
        SchedulePeriod period,
        Employee employee,
        List<(int EmployeeId, DateOnly WorkDate, int ShiftTypeId, AssignmentSource Source)> candidateList,
        IReadOnlyList<ShiftType> allShifts)
    {
        var candidateAssignments = candidateList.Select(c => new ShiftAssignment
        {
            EmployeeId = c.EmployeeId,
            WorkDate = c.WorkDate,
            ShiftTypeId = c.ShiftTypeId,
            Source = c.Source
        }).ToList();

        var empValidationErrors = new List<string>();
        var userAssigns = candidateAssignments.Where(a => a.EmployeeId == employee.EmployeeId).ToList();

        ValidateEmployeeWorkloadRules(employee, userAssigns, allShifts, empValidationErrors);
        if (empValidationErrors.Any())
            return (false, ServiceResult.Failure(ErrorCodes.InvalidShiftAssignment, empValidationErrors.First()));

        var shiftTypeMap = allShifts.ToDictionary(s => s.ShiftTypeId);
        var restErrors = SchedulingRestRuleEvaluator.EvaluateEmployeeRestRules(
            employee, userAssigns, shiftTypeMap, period.WeekStartDate);
        if (restErrors.Count > 0)
            return (false, ServiceResult.Failure(ErrorCodes.InvalidShiftAssignment, restErrors.First()));

        return (true, null);
    }

    private async Task<ServiceResult> SaveManualAssignmentAsync(
        WorkSchedule schedule,
        Employee employee,
        ManualAssignRequestViewModel model,
        List<(int EmployeeId, DateOnly WorkDate, int ShiftTypeId, AssignmentSource Source)> candidateList,
        int accountId,
        CancellationToken cancellationToken)
    {
        await _workScheduleRepository.SaveAssignmentsAsync(
            schedule.WorkScheduleId, candidateList, _clock.UtcNow, cancellationToken);
        _auditLogRepository.Add(AuditLogFactory.Create(
            accountId, "MANUAL_ASSIGN_EMPLOYEE", nameof(ShiftAssignment),
            $"{employee.EmployeeCode}_{model.WorkDate:yyyyMMdd}_{model.ShiftTypeId}", null,
            new { model.SchedulePeriodId, model.EmployeeId, model.WorkDate, model.ShiftTypeId }, _clock.UtcNow));

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Manual assignment created. ActorAccountId: {ActorAccountId}; EmployeeCode: {EmployeeCode}",
            accountId, employee.EmployeeCode);
        return ServiceResult.Success();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> RemoveEmployeeManualAsync(
        ManualRemoveRequestViewModel model,
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var schedule = await _workScheduleRepository.GetByPeriodIdAsync(
            model.SchedulePeriodId, false, true, cancellationToken);
        if (schedule is null || schedule.Status == ScheduleStatus.Published)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidScheduleStatus, "Không tìm thấy lịch nháp hoặc lịch đã công bố.");
        }

        var assignment = schedule.ShiftAssignments.FirstOrDefault(
            a => a.ShiftAssignmentId == model.ShiftAssignmentId);
        if (assignment is null)
            return ServiceResult.Failure(ErrorCodes.ShiftAssignmentNotFound, "Không tìm thấy phân công ca.");

        var newList = schedule.ShiftAssignments.Where(a => a.ShiftAssignmentId != model.ShiftAssignmentId)
            .Select(a => (a.EmployeeId, a.WorkDate, a.ShiftTypeId, a.Source)).ToList();

        await _workScheduleRepository.SaveAssignmentsAsync(
            schedule.WorkScheduleId, newList, _clock.UtcNow, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> SaveDraftAsync(
        int schedulePeriodId,
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var schedule = await _workScheduleRepository.GetByPeriodIdAsync(
            schedulePeriodId, false, true, cancellationToken);
        if (schedule is null)
            return ServiceResult.Failure(ErrorCodes.SchedulePeriodNotFound, "Không tìm thấy lịch nháp.");

        schedule.UpdatedAt = _clock.UtcNow;
        _auditLogRepository.Add(AuditLogFactory.Create(
            accountId, "SAVE_DRAFT_SCHEDULE", nameof(WorkSchedule), schedule.WorkScheduleId.ToString(), null,
            new { schedulePeriodId, Count = schedule.ShiftAssignments.Count }, _clock.UtcNow));

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    private static void ValidateEmployeeWorkloadRules(
        Employee emp,
        List<ShiftAssignment> userAssigns,
        IReadOnlyList<ShiftType> shiftTypes,
        List<string> hardErrors)
    {
        var dayGroups = userAssigns.GroupBy(a => a.WorkDate).ToList();
        if (emp.EmploymentType == EmploymentType.PartTime)
            ValidatePartTimeWorkloadRules(emp, dayGroups, hardErrors);
        else if (emp.EmploymentType == EmploymentType.FullTime)
            ValidateFullTimeWorkloadRules(emp, dayGroups, shiftTypes, hardErrors);
    }

    private static void ValidatePartTimeWorkloadRules(
        Employee emp,
        List<IGrouping<DateOnly, ShiftAssignment>> dayGroups,
        List<string> hardErrors)
    {
        foreach (var group in dayGroups)
        {
            if (group.Count() > 1)
            {
                hardErrors.Add($"Nhân viên PT {emp.FullName} ({emp.EmployeeCode})"
                    + " chỉ được phân tối đa 1 ca/ngày.");
            }
        }
    }

    private static void ValidateFullTimeWorkloadRules(
        Employee emp,
        List<IGrouping<DateOnly, ShiftAssignment>> dayGroups,
        IReadOnlyList<ShiftType> shiftTypes,
        List<string> hardErrors)
    {
        if (dayGroups.Count > 6)
        {
            hardErrors.Add($"Nhân viên FT {emp.FullName} ({emp.EmployeeCode})"
                + " bị vượt quá 6 ngày làm việc trong tuần.");
        }

        var shiftCodeMap = shiftTypes.ToDictionary(s => s.ShiftTypeId, s => s.ShiftCode);
        foreach (var group in dayGroups)
        {
            var codes = group.Select(a => shiftCodeMap.TryGetValue(a.ShiftTypeId, out var sc) ? sc : "").ToHashSet();
            var isMorningAfternoon = codes.Contains(ShiftTypeConstants.MorningCode)
                && codes.Contains(ShiftTypeConstants.AfternoonCode) && codes.Count == 2;
            var isAfternoonEvening = codes.Contains(ShiftTypeConstants.AfternoonCode)
                && codes.Contains(ShiftTypeConstants.EveningCode) && codes.Count == 2;

            if (!isMorningAfternoon && !isAfternoonEvening)
            {
                hardErrors.Add($"Nhân viên FT {emp.FullName} ({emp.EmployeeCode}) " +
                    "trong ngày phải làm 2 ca liên tiếp (Sáng-Chiều hoặc Chiều-Tối).");
            }
        }
    }
}
