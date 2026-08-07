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
        if (period is null)
        {
            return new List<EligibleEmployeeOptionViewModel>();
        }

        var activeEmployees = await _employeeRepository.GetAllActiveAsync(cancellationToken);
        var availabilities = await _availabilityRepository.GetByPeriodWithDetailsAsync(
            schedulePeriodId, null, null, workDate, shiftTypeId, cancellationToken);

        var leaves = await _leaveRequestRepository.GetApprovedLeavesByPeriodAsync(
            schedulePeriodId, cancellationToken);

        var schedule = await _workScheduleRepository.GetByPeriodIdAsync(
            schedulePeriodId, true, true, cancellationToken);

        var currentAssignments = schedule?.ShiftAssignments.ToList() ?? new List<ShiftAssignment>();
        var availSet = availabilities.Select(a => a.EmployeeId).ToHashSet();
        var leaveSet = leaves.Where(l => l.Status == LeaveRequestStatus.Approved).ToList();

        var result = new List<EligibleEmployeeOptionViewModel>();
        foreach (var emp in activeEmployees)
        {
            var hasAvail = availSet.Contains(emp.EmployeeId);
            var hasLeave = leaveSet.Any(l => l.EmployeeId == emp.EmployeeId
                && l.LeaveDate == workDate
                && (!l.ShiftTypeId.HasValue || l.ShiftTypeId.Value == shiftTypeId));

            var hasOverlap = currentAssignments.Any(a => a.EmployeeId == emp.EmployeeId
                && a.WorkDate == workDate && a.ShiftTypeId == shiftTypeId);

            var reasons = new List<string>();
            if (!hasAvail) reasons.Add("Chưa đăng ký rảnh");
            if (hasLeave) reasons.Add("Có đơn nghỉ đã duyệt");
            if (hasOverlap) reasons.Add("Đã được phân vào ca này");

            var isEligible = hasAvail && !hasLeave && !hasOverlap;

            result.Add(new EligibleEmployeeOptionViewModel
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
            });
        }

        return result.OrderByDescending(e => e.IsEligible)
                     .ThenBy(e => e.Position)
                     .ThenBy(e => e.FullName)
                     .ToList();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> AssignEmployeeManualAsync(
        ManualAssignRequestViewModel model,
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var period = await _periodRepository.GetByIdAsync(model.SchedulePeriodId, true, true, cancellationToken);
        if (period is null)
        {
            return ServiceResult.Failure(ErrorCodes.SchedulePeriodNotFound, "Không tìm thấy kỳ lịch.");
        }

        var schedule = await _workScheduleRepository.EnsureDraftExistsAsync(
            model.SchedulePeriodId, accountId, _clock.UtcNow, cancellationToken);

        schedule = await _workScheduleRepository.GetByPeriodIdAsync(
            model.SchedulePeriodId, false, true, cancellationToken);

        if (schedule is null || schedule.Status == ScheduleStatus.Published)
        {
            return ServiceResult.Failure(ErrorCodes.InvalidScheduleStatus, "Lịch đã công bố không thể chỉnh sửa.");
        }

        var employee = await _employeeRepository.GetByIdAsync(model.EmployeeId, true, cancellationToken);
        if (employee is null || !employee.IsActive)
        {
            return ServiceResult.Failure(ErrorCodes.EmployeeNotFound, "Nhân viên không tồn tại hoặc đã ngưng hoạt động.");
        }

        // BR05: Kiểm tra lịch rảnh
        var avails = await _availabilityRepository.GetByEmployeeAndPeriodAsync(
            employee.EmployeeId, model.SchedulePeriodId, cancellationToken);
        if (!avails.Any(a => a.WorkDate == model.WorkDate && a.ShiftTypeId == model.ShiftTypeId))
        {
            return ServiceResult.Failure(ErrorCodes.EmployeeNotAvailable, $"Nhân viên {employee.FullName} chưa đăng ký rảnh cho ca này.");
        }

        // BR06: Kiểm tra đơn nghỉ đã duyệt
        var leaves = await _leaveRequestRepository.GetApprovedLeavesByPeriodAsync(
            model.SchedulePeriodId, cancellationToken);
        if (leaves.Any(l => l.EmployeeId == employee.EmployeeId && l.LeaveDate == model.WorkDate && (!l.ShiftTypeId.HasValue || l.ShiftTypeId.Value == model.ShiftTypeId)))
        {
            return ServiceResult.Failure(ErrorCodes.EmployeeOnLeave, $"Nhân viên {employee.FullName} có đơn xin nghỉ đã được duyệt.");
        }

        var currentList = schedule.ShiftAssignments.ToList();

        // Xử lý thay thế nhân viên nếu có
        if (model.ReplaceShiftAssignmentId.HasValue && model.ReplaceShiftAssignmentId.Value > 0)
        {
            currentList.RemoveAll(a => a.ShiftAssignmentId == model.ReplaceShiftAssignmentId.Value);
        }

        // BR08: Kiểm tra trùng ca
        if (currentList.Any(a => a.EmployeeId == employee.EmployeeId && a.WorkDate == model.WorkDate && a.ShiftTypeId == model.ShiftTypeId))
        {
            return ServiceResult.Failure(ErrorCodes.ShiftAssignmentOverlap, $"Nhân viên {employee.FullName} đã được phân vào ca này.");
        }

        // Tạo danh sách assignments giả lập để kiểm tra BR09, BR10, BR11
        var candidateList = currentList.Select(a => (a.EmployeeId, a.WorkDate, a.ShiftTypeId, a.Source)).ToList();
        candidateList.Add((employee.EmployeeId, model.WorkDate, model.ShiftTypeId, AssignmentSource.Manual));

        var allShifts = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);
        var activeEmployees = await _employeeRepository.GetAllActiveAsync(cancellationToken);

        var candidateAssignments = candidateList.Select(c => new ShiftAssignment
        {
            EmployeeId = c.EmployeeId,
            WorkDate = c.WorkDate,
            ShiftTypeId = c.ShiftTypeId,
            Source = c.Source
        }).ToList();

        // BR09/10/11 Single Employee Workload Check
        var empValidationErrors = new List<string>();
        var userAssigns = candidateAssignments.Where(a => a.EmployeeId == employee.EmployeeId).ToList();

        ValidateEmployeeWorkloadRules(employee, userAssigns, allShifts, empValidationErrors);
        if (empValidationErrors.Any())
        {
            return ServiceResult.Failure(ErrorCodes.InvalidShiftAssignment, empValidationErrors.First());
        }

        await _workScheduleRepository.SaveAssignmentsAsync(
            schedule.WorkScheduleId, candidateList, _clock.UtcNow, cancellationToken);

        _auditLogRepository.Add(AuditLogFactory.Create(
            accountId,
            "MANUAL_ASSIGN_EMPLOYEE",
            nameof(ShiftAssignment),
            $"{employee.EmployeeCode}_{model.WorkDate:yyyyMMdd}_{model.ShiftTypeId}",
            null,
            new { model.SchedulePeriodId, model.EmployeeId, model.WorkDate, model.ShiftTypeId },
            _clock.UtcNow));

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
            return ServiceResult.Failure(ErrorCodes.InvalidScheduleStatus, "Không tìm thấy lịch nháp hoặc lịch đã công bố.");
        }

        var assignment = schedule.ShiftAssignments.FirstOrDefault(a => a.ShiftAssignmentId == model.ShiftAssignmentId);
        if (assignment is null)
        {
            return ServiceResult.Failure(ErrorCodes.ShiftAssignmentNotFound, "Không tìm thấy phân công ca.");
        }

        var newList = schedule.ShiftAssignments
            .Where(a => a.ShiftAssignmentId != model.ShiftAssignmentId)
            .Select(a => (a.EmployeeId, a.WorkDate, a.ShiftTypeId, a.Source))
            .ToList();

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
        {
            return ServiceResult.Failure(ErrorCodes.SchedulePeriodNotFound, "Không tìm thấy lịch nháp.");
        }

        // BR14: Cho phép lưu lịch nháp dù chưa hoàn chỉnh
        schedule.UpdatedAt = _clock.UtcNow;
        _auditLogRepository.Add(AuditLogFactory.Create(
            accountId,
            "SAVE_DRAFT_SCHEDULE",
            nameof(WorkSchedule),
            schedule.WorkScheduleId.ToString(),
            null,
            new { schedulePeriodId, Count = schedule.ShiftAssignments.Count },
            _clock.UtcNow));

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    private static void ValidateEmployeeWorkloadRules(
        Employee emp,
        List<ShiftAssignment> userAssigns,
        IReadOnlyList<ShiftType> shiftTypes,
        List<string> hardErrors)
    {
        var shiftCodeMap = shiftTypes.ToDictionary(s => s.ShiftTypeId, s => s.ShiftCode);
        var dayGroups = userAssigns.GroupBy(a => a.WorkDate).ToList();

        if (emp.EmploymentType == EmploymentType.PartTime)
        {
            // BR09: Bán thời gian max 1 ca/ngày
            foreach (var group in dayGroups)
            {
                if (group.Count() > 1)
                {
                    hardErrors.Add($"Nhân viên PT {emp.FullName} ({emp.EmployeeCode}) chỉ được phân tối đa 1 ca/ngày.");
                }
            }
        }
        else if (emp.EmploymentType == EmploymentType.FullTime)
        {
            // BR11: Toàn thời gian max 6 ngày/tuần
            if (dayGroups.Count > 6)
            {
                hardErrors.Add($"Nhân viên FT {emp.FullName} ({emp.EmployeeCode}) bị vượt quá 6 ngày làm việc trong tuần.");
            }

            // BR10: FT 2 ca liên tiếp (Sáng-Chiều hoặc Chiều-Tối)
            foreach (var group in dayGroups)
            {
                var codes = group.Select(a => shiftCodeMap.TryGetValue(a.ShiftTypeId, out var sc) ? sc : "").ToHashSet();
                var isMorningAfternoon = codes.Contains(ShiftTypeConstants.MorningCode) && codes.Contains(ShiftTypeConstants.AfternoonCode) && codes.Count == 2;
                var isAfternoonEvening = codes.Contains(ShiftTypeConstants.AfternoonCode) && codes.Contains(ShiftTypeConstants.EveningCode) && codes.Count == 2;

                if (!isMorningAfternoon && !isAfternoonEvening)
                {
                    hardErrors.Add($"Nhân viên FT {emp.FullName} ({emp.EmployeeCode}) trong ngày phải làm 2 ca liên tiếp (Sáng-Chiều hoặc Chiều-Tối).");
                }
            }
        }
    }
}
