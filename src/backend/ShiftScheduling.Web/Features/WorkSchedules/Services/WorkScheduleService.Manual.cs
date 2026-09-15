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

        var req = period.StaffingRequirements.FirstOrDefault(r =>
            r.WorkDate == model.WorkDate && r.ShiftTypeId == model.ShiftTypeId);
        if (req is null)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidShiftAssignment,
                "Không tìm thấy định mức nhân sự cho ca này.");
        }

        var activeEmployees = await _employeeRepository.GetAllActiveAsync(cancellationToken);
        var empMap = activeEmployees.ToDictionary(e => e.EmployeeId);

        var shiftCandidates = candidateList
            .Where(c => c.WorkDate == model.WorkDate && c.ShiftTypeId == model.ShiftTypeId)
            .ToList();

        int candidateSales = 0;
        int candidateCashiers = 0;

        foreach (var c in shiftCandidates)
        {
            EmployeePosition pos;
            if (c.EmployeeId == employee.EmployeeId)
            {
                pos = employee.Position;
            }
            else if (empMap.TryGetValue(c.EmployeeId, out var emp))
            {
                pos = emp.Position;
            }
            else
            {
                var fetched = await _employeeRepository.GetByIdAsync(c.EmployeeId, false, cancellationToken);
                if (fetched is null)
                {
                    return ServiceResult.Failure(
                        ErrorCodes.InvalidShiftAssignment,
                        "Không thể xác định vị trí làm việc của nhân viên trong ca.");
                }
                pos = fetched.Position;
            }

            if (pos == EmployeePosition.Sales) candidateSales++;
            else if (pos == EmployeePosition.Cashier) candidateCashiers++;
        }

        if (candidateSales > req.RequiredSalesStaff)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidShiftAssignment,
                $"Ca này đã đủ định mức nhân viên Tư vấn ({req.RequiredSalesStaff}/{req.RequiredSalesStaff}).");
        }

        if (candidateCashiers > req.RequiredCashiers)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidShiftAssignment,
                $"Ca này đã đủ định mức nhân viên Thu ngân ({req.RequiredCashiers}/{req.RequiredCashiers}).");
        }

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
        if (schedule is null || schedule.Status != ScheduleStatus.Draft)
        {
            return (false, null, ServiceResult.Failure(
                ErrorCodes.InvalidScheduleStatus, "Lịch không ở trạng thái nháp nên không thể chỉnh sửa."));
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

        var periodShifts = DailyShiftRuleEvaluator.ExtractPeriodShifts(period.StaffingRequirements, allShifts);
        if (!DailyShiftRuleEvaluator.TryResolvePolicy(periodShifts, out var policy, out var policyError))
        {
            return (false, ServiceResult.Failure(ErrorCodes.ShiftConfigurationInvalid, policyError ?? "Cấu hình ca của kỳ lịch không hợp lệ."));
        }

        var (policyMax, allowSplit) = policy;
        ValidateEmployeeWorkloadRules(employee, userAssigns, allShifts, policyMax, allowSplit, empValidationErrors);
        if (empValidationErrors.Any())
            return (false, ServiceResult.Failure(ErrorCodes.InvalidShiftAssignment, empValidationErrors.First()));

        var shiftTypeMap = allShifts.ToDictionary(s => s.ShiftTypeId);

        // BR-05: Tổng giờ làm trong tuần không được vượt trần theo loại hợp đồng.
        var hoursErrors = WeeklyWorkHoursRuleEvaluator.EvaluateWeeklyWorkHours(
            employee, userAssigns, shiftTypeMap, period.WeekStartDate, period.WeekEndDate);
        if (hoursErrors.Count > 0)
            return (false, ServiceResult.Failure(ErrorCodes.InvalidShiftAssignment, hoursErrors.First()));

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
        if (schedule is null || schedule.Status != ScheduleStatus.Draft)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidScheduleStatus, "Lịch không ở trạng thái nháp nên không thể bỏ phân công.");
        }

        var assignment = schedule.ShiftAssignments.FirstOrDefault(
            a => a.ShiftAssignmentId == model.ShiftAssignmentId);
        if (assignment is null)
            return ServiceResult.Failure(ErrorCodes.ShiftAssignmentNotFound, "Không tìm thấy phân công ca.");

        var newList = schedule.ShiftAssignments.Where(a => a.ShiftAssignmentId != model.ShiftAssignmentId)
            .Select(a => (a.EmployeeId, a.WorkDate, a.ShiftTypeId, a.Source)).ToList();

        var now = _clock.UtcNow;
        await _workScheduleRepository.SaveAssignmentsAsync(
            schedule.WorkScheduleId, newList, now, cancellationToken);
        _auditLogRepository.Add(AuditLogFactory.Create(
            accountId,
            "MANUAL_REMOVE_EMPLOYEE",
            nameof(ShiftAssignment),
            assignment.ShiftAssignmentId.ToString(),
            new
            {
                schedule.SchedulePeriodId,
                schedule.WorkScheduleId,
                assignment.ShiftAssignmentId,
                assignment.EmployeeId,
                assignment.WorkDate,
                assignment.ShiftTypeId,
                assignment.Source
            },
            null,
            now));

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

        if (schedule.Status != ScheduleStatus.Draft)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidScheduleStatus, "Chỉ có thể lưu nháp cho lịch làm việc ở trạng thái Bản nháp.");
        }

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
        int policyMax,
        bool allowSplit,
        List<string> hardErrors)
    {
        var dayGroups = userAssigns.GroupBy(a => a.WorkDate).ToList();

        if (emp.EmploymentType == EmploymentType.FullTime && dayGroups.Count > 6)
        {
            hardErrors.Add($"Nhân viên FT {emp.FullName} ({emp.EmployeeCode})"
                + " bị vượt quá 6 ngày làm việc trong tuần.");
        }

        var shiftTypeMap = shiftTypes.ToDictionary(s => s.ShiftTypeId);
        foreach (var group in dayGroups)
        {
            var dayShifts = group
                .Select(a => shiftTypeMap.TryGetValue(a.ShiftTypeId, out var sc) ? sc : null)
                .Where(s => s != null)
                .ToList();

            DailyShiftRuleEvaluator.ValidateDailyAssignments(emp, group.Key, dayShifts!, policyMax, allowSplit, hardErrors);
        }
    }
}
