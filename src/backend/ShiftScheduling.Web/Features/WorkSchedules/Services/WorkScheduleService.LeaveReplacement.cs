using System.Globalization;
using Microsoft.Extensions.Logging;
using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Notifications;
using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.WorkSchedules.Services;

public partial class WorkScheduleService
{
    /// <inheritdoc />
    public async Task<LeaveReplacementViewModel?> GetLeaveReplacementAsync(
        int leaveRequestId, CancellationToken cancellationToken = default)
    {
        var leaveRequest = await _leaveRequestRepository.GetByIdAsync(leaveRequestId, true, cancellationToken);
        if (leaveRequest is null || leaveRequest.Status != LeaveRequestStatus.Approved) return null;

        return await BuildLeaveReplacementCoreAsync(leaveRequest, cancellationToken);
    }

    /// <summary>
    /// Dựng danh sách ca bị ảnh hưởng và ứng viên thay thế cho một đơn nghỉ trên lịch đã công bố.
    /// Hệ thống chỉ lọc ứng viên hợp lệ; Quản lý vẫn phải chủ động chọn và xác nhận người thay.
    /// </summary>
    private async Task<LeaveReplacementViewModel?> BuildLeaveReplacementCoreAsync(
        LeaveRequest leaveRequest, CancellationToken cancellationToken)
    {
        var period = await _periodRepository.GetByIdAsync(
            leaveRequest.SchedulePeriodId, true, true, cancellationToken);
        var schedule = await _workScheduleRepository.GetByPeriodIdAsync(
            leaveRequest.SchedulePeriodId, true, true, cancellationToken);
        if (period is null || schedule is null || schedule.Status != ScheduleStatus.Published) return null;

        var activeEmployees = await _employeeRepository.GetAllActiveAsync(cancellationToken);
        var approvedLeaves = await _leaveRequestRepository.GetApprovedLeavesByPeriodAsync(
            period.SchedulePeriodId, cancellationToken);
        var allShifts = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);
        var shiftTypeMap = allShifts.ToDictionary(s => s.ShiftTypeId);

        var affectedAssignments = schedule.ShiftAssignments
            .Where(a => a.EmployeeId == leaveRequest.EmployeeId && a.WorkDate == leaveRequest.LeaveDate
                && (!leaveRequest.ShiftTypeId.HasValue || a.ShiftTypeId == leaveRequest.ShiftTypeId.Value))
            .OrderBy(a => a.ShiftType?.StartTime ?? TimeOnly.MinValue).ToList();

        var assignmentViewModels = new List<LeaveReplacementAssignmentViewModel>(affectedAssignments.Count);
        foreach (var assignment in affectedAssignments)
        {
            var shiftType = assignment.ShiftType
                ?? (shiftTypeMap.TryGetValue(assignment.ShiftTypeId, out var st) ? st : null);

            var isEnded = shiftType is null || HasShiftEnded(assignment.WorkDate, shiftType);
            var candidates = (shiftType is null || isEnded)
                ? new List<LeaveReplacementCandidateViewModel>()
                : await BuildReplacementCandidatesAsync(
                    period, schedule, assignment, leaveRequest.Employee,
                    activeEmployees, approvedLeaves, allShifts, shiftTypeMap, cancellationToken);

            assignmentViewModels.Add(new LeaveReplacementAssignmentViewModel
            {
                ShiftAssignmentId = assignment.ShiftAssignmentId,
                WorkScheduleId = assignment.WorkScheduleId,
                WorkDate = assignment.WorkDate,
                ShiftTypeId = assignment.ShiftTypeId,
                ShiftName = shiftType?.ShiftName ?? $"Ca {assignment.ShiftTypeId}",
                StartTime = shiftType?.StartTime ?? TimeOnly.MinValue,
                EndTime = shiftType?.EndTime ?? TimeOnly.MinValue,
                RequiredPosition = leaveRequest.Employee.Position,
                IsEnded = isEnded,
                Candidates = candidates
            });
        }

        var result = CreateLeaveReplacementViewModel(leaveRequest, period, assignmentViewModels);
        result.LeaveStatus = leaveRequest.Status;
        result.RowVersion = Convert.ToBase64String(leaveRequest.RowVersion);
        result.ManagerNote = leaveRequest.ManagerNote;
        return result;
    }

    private static LeaveReplacementViewModel CreateLeaveReplacementViewModel(
        LeaveRequest leaveRequest, SchedulePeriod period,
        List<LeaveReplacementAssignmentViewModel> assignments) => new()
        {
            LeaveRequestId = leaveRequest.LeaveRequestId,
            SchedulePeriodId = period.SchedulePeriodId,
            PeriodName = period.PeriodName,
            EmployeeId = leaveRequest.EmployeeId,
            EmployeeCode = leaveRequest.Employee.EmployeeCode,
            EmployeeFullName = leaveRequest.Employee.FullName,
            EmployeePosition = leaveRequest.Employee.Position,
            LeaveDate = leaveRequest.LeaveDate,
            ShiftTypeId = leaveRequest.ShiftTypeId,
            ShiftTypeName = leaveRequest.ShiftType?.ShiftName,
            LeaveReason = leaveRequest.Reason,
            AffectedAssignments = assignments
        };

    /// <inheritdoc />
    public async Task<ServiceResult> ReplaceApprovedLeaveAssignmentAsync(
        LeaveReplacementRequestViewModel model, int managerAccountId, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateLeaveAndTargetAssignmentAsync(model, cancellationToken);
        if (!validation.Succeeded) return validation.Result!;

        var (leaveRequest, period, schedule, targetAssignment) = validation.Data!.Value;
        var candidateCheck = await ValidateCandidateForReplacementAsync(
            period, schedule, targetAssignment, leaveRequest.Employee, model.CandidateEmployeeId, cancellationToken);
        if (!candidateCheck.Succeeded) return candidateCheck.Result!;

        return await ExecuteReplacementMutationAsync(
            schedule, targetAssignment, leaveRequest, candidateCheck.Candidate!, managerAccountId, cancellationToken);
    }

    private async Task<List<LeaveReplacementCandidateViewModel>> BuildReplacementCandidatesAsync(
        SchedulePeriod period, WorkSchedule schedule, ShiftAssignment target, Employee leavingEmployee,
        IReadOnlyList<Employee> activeEmployees, IReadOnlyList<LeaveRequest> approvedLeaves,
        IReadOnlyList<ShiftType> allShifts, Dictionary<int, ShiftType> shiftTypeMap,
        CancellationToken cancellationToken)
    {
        // BR07: Người thay phải cùng vị trí công việc với nhân viên nghỉ.
        var samePositionEmployees = activeEmployees.Where(
            e => e.EmployeeId != leavingEmployee.EmployeeId && e.Position == leavingEmployee.Position).ToList();

        // BR05/BR06: Chỉ xét nhân viên đã đăng ký rảnh và không có đơn nghỉ đã duyệt trùng ca.
        var availabilities = await _availabilityRepository.GetByPeriodWithDetailsAsync(
            period.SchedulePeriodId, null, null, target.WorkDate, target.ShiftTypeId, cancellationToken);
        var availMap = availabilities.ToDictionary(a => a.EmployeeId, a => a.PreferenceLevel);

        var candidateOptions = new List<LeaveReplacementCandidateViewModel>(samePositionEmployees.Count);
        foreach (var candidate in samePositionEmployees)
        {
            if (!availMap.TryGetValue(candidate.EmployeeId, out var preferenceLevel)) continue;
            if (!IsCandidateEligible(period, schedule, target, candidate,
                approvedLeaves, allShifts, shiftTypeMap, activeEmployees)) continue;

            candidateOptions.Add(new LeaveReplacementCandidateViewModel
            {
                EmployeeId = candidate.EmployeeId,
                EmployeeCode = candidate.EmployeeCode,
                FullName = candidate.FullName,
                Position = candidate.Position,
                EmploymentType = candidate.EmploymentType,
                Level = candidate.Level,
                PreferenceLevel = preferenceLevel == AvailabilityPreferenceLevel.Preferred
                    ? AvailabilityPreferenceLevel.Preferred : AvailabilityPreferenceLevel.Normal
            });
        }

        return candidateOptions
            .OrderByDescending(c => c.PreferenceLevel == AvailabilityPreferenceLevel.Preferred)
            .ThenBy(c => c.EmployeeCode, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsCandidateEligible(
        SchedulePeriod period, WorkSchedule schedule, ShiftAssignment target, Employee candidate,
        IReadOnlyList<LeaveRequest> approvedLeaves, IReadOnlyList<ShiftType> allShifts,
        Dictionary<int, ShiftType> shiftTypeMap, IReadOnlyList<Employee> activeEmployees)
    {
        var hasLeave = approvedLeaves.Any(l => l.EmployeeId == candidate.EmployeeId
            && l.LeaveDate == target.WorkDate
            && (!l.ShiftTypeId.HasValue || l.ShiftTypeId.Value == target.ShiftTypeId));
        if (hasLeave) return false;

        // BR09–BR11, NC-01/BR-05, BR20/BR21: Tái sử dụng bộ quy tắc phân công hiện có.
        var projectedTuples = schedule.ShiftAssignments
            .Where(a => a.ShiftAssignmentId != target.ShiftAssignmentId)
            .Select(a => (a.EmployeeId, a.WorkDate, a.ShiftTypeId, a.Source))
            .Append((candidate.EmployeeId, target.WorkDate, target.ShiftTypeId, AssignmentSource.Manual))
            .ToList();

        var (rulesSucceeded, _) = ValidateCandidateRules(period, candidate, projectedTuples, allShifts);
        if (!rulesSucceeded) return false;

        // BR12/BR13: Kiểm tra định mức nhân sự và nhân viên kinh nghiệm sau khi giả lập người thay.
        return EvaluateShiftStaffingComposition(period, schedule, target, candidate, shiftTypeMap, activeEmployees);
    }

    private static bool EvaluateShiftStaffingComposition(
        SchedulePeriod period, WorkSchedule schedule, ShiftAssignment target, Employee candidate,
        Dictionary<int, ShiftType> shiftTypeMap, IReadOnlyList<Employee> activeEmployees)
    {
        var staffingRequirement = period.StaffingRequirements.FirstOrDefault(r =>
            r.WorkDate == target.WorkDate && r.ShiftTypeId == target.ShiftTypeId);
        if (staffingRequirement is null) return false;

        var projected = schedule.ShiftAssignments
            .Where(a => a.ShiftAssignmentId != target.ShiftAssignmentId
                && a.WorkDate == target.WorkDate && a.ShiftTypeId == target.ShiftTypeId)
            .Select(a => new ShiftAssignment
            {
                EmployeeId = a.EmployeeId,
                WorkDate = a.WorkDate,
                ShiftTypeId = a.ShiftTypeId
            })
            .Append(new ShiftAssignment
            {
                EmployeeId = candidate.EmployeeId,
                WorkDate = target.WorkDate,
                ShiftTypeId = target.ShiftTypeId,
                Source = AssignmentSource.Manual
            })
            .ToList();

        var hardErrors = new List<string>();
        var employeeMap = activeEmployees.ToDictionary(e => e.EmployeeId);
        SchedulingValidationService.ValidateSingleShiftStaffing(
            staffingRequirement, projected, employeeMap, shiftTypeMap, hardErrors);
        return hardErrors.Count == 0;
    }

    private async Task<(bool Succeeded, (LeaveRequest Leave, SchedulePeriod Period,
        WorkSchedule Schedule, ShiftAssignment Target)? Data, ServiceResult? Result)>
        ValidateLeaveAndTargetAssignmentAsync(
            LeaveReplacementRequestViewModel model, CancellationToken cancellationToken)
    {
        var leave = await _leaveRequestRepository.GetByIdAsync(model.LeaveRequestId, false, cancellationToken);
        if (leave is null || leave.Status != LeaveRequestStatus.Approved)
            return (false, null, ServiceResult.Failure(
                ErrorCodes.LeaveRequestNotFound, "Không tìm thấy đơn xin nghỉ đã duyệt."));

        var period = await _periodRepository.GetByIdAsync(leave.SchedulePeriodId, true, true, cancellationToken);
        var schedule = await _workScheduleRepository.GetByPeriodIdAsync(
            leave.SchedulePeriodId, false, true, cancellationToken);
        if (period is null || schedule is null || schedule.Status != ScheduleStatus.Published)
            return (false, null, ServiceResult.Failure(
                ErrorCodes.InvalidScheduleStatus, "Chỉ có thể thay thế ca trên lịch đã công bố."));

        var target = schedule.ShiftAssignments.FirstOrDefault(a => a.ShiftAssignmentId == model.ShiftAssignmentId);
        if (target is null)
            return (false, null, ServiceResult.Failure(
                ErrorCodes.ShiftAssignmentNotFound, "Không tìm thấy phân công ca."));
        if (target.EmployeeId != leave.EmployeeId || target.WorkDate != leave.LeaveDate
            || (leave.ShiftTypeId.HasValue && target.ShiftTypeId != leave.ShiftTypeId.Value))
            return (false, null, ServiceResult.Failure(
                ErrorCodes.InvalidShiftAssignment, "Ca phân công không khớp với đơn xin nghỉ."));

        if (target.ActualWorkedHours.HasValue || !string.IsNullOrEmpty(target.WorkdayAdjustmentReason))
            return (false, null, ServiceResult.Failure(
                ErrorCodes.InvalidShiftAssignment,
                "Ca làm việc đã có dữ liệu điều chỉnh giờ công, không thể thay thế nhân sự."));

        var shiftType = target.ShiftType
            ?? await _shiftTypeRepository.GetByIdAsync(target.ShiftTypeId, true, cancellationToken);
        if (shiftType is null)
            return (false, null, ServiceResult.Failure(
                ErrorCodes.ShiftTypeNotFound, "Không tìm thấy thông tin ca làm việc."));

        if (HasShiftEnded(target.WorkDate, shiftType))
            return (false, null, ServiceResult.Failure(
                ErrorCodes.LeaveRequestShiftEnded, "Ca làm việc đã kết thúc, không thể xếp người thay."));

        target.ShiftType = shiftType;
        return (true, (leave, period, schedule, target), null);
    }

    private async Task<(bool Succeeded, Employee? Candidate, ServiceResult? Result)>
        ValidateCandidateForReplacementAsync(
            SchedulePeriod period, WorkSchedule schedule, ShiftAssignment target,
            Employee leavingEmployee, int candidateId, CancellationToken cancellationToken)
    {
        // BR05/BR06: Tái sử dụng helper để kiểm tra nhân viên hoạt động,
        // đăng ký rảnh và đơn nghỉ đã duyệt theo dữ liệu mới nhất.
        var dummyModel = new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            WorkDate = target.WorkDate,
            ShiftTypeId = target.ShiftTypeId,
            EmployeeId = candidateId
        };
        var availLeaveCheck = await ValidateEmployeeAvailabilityAndLeavesAsync(dummyModel, cancellationToken);
        if (!availLeaveCheck.Succeeded) return (false, null, availLeaveCheck.Result);

        var candidate = availLeaveCheck.Employee!;
        if (candidate.EmployeeId == leavingEmployee.EmployeeId)
            return (false, null, ServiceResult.Failure(
                ErrorCodes.InvalidShiftAssignment, "Nhân viên thay thế không thể là nhân viên đang nghỉ."));

        // BR07: Người thay phải cùng vị trí công việc với nhân viên nghỉ.
        if (candidate.Position != leavingEmployee.Position)
            return (false, null, ServiceResult.Failure(
                ErrorCodes.InvalidShiftAssignment,
                "Nhân viên thay thế phải cùng vị trí công việc với nhân viên đang nghỉ."));

        var allShifts = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);

        // BR09–BR11, NC-01/BR-05, BR20/BR21: Giả lập ca in-memory và gọi bộ quy tắc phân công.
        var projectedTuples = schedule.ShiftAssignments
            .Where(a => a.ShiftAssignmentId != target.ShiftAssignmentId)
            .Select(a => (a.EmployeeId, a.WorkDate, a.ShiftTypeId, a.Source))
            .Append((candidate.EmployeeId, target.WorkDate, target.ShiftTypeId, AssignmentSource.Manual))
            .ToList();

        var (rulesOk, rulesResult) = ValidateCandidateRules(period, candidate, projectedTuples, allShifts);
        if (!rulesOk) return (false, null, rulesResult);

        // BR12/BR13: Kiểm tra định mức vị trí và nhân viên Level 2+ cho ca làm việc.
        var activeEmployees = await _employeeRepository.GetAllActiveAsync(cancellationToken);
        var shiftTypeMap = allShifts.ToDictionary(s => s.ShiftTypeId);
        var staffingOk = EvaluateShiftStaffingComposition(
            period, schedule, target, candidate, shiftTypeMap, activeEmployees);
        if (!staffingOk)
            return (false, null, ServiceResult.Failure(
                ErrorCodes.InvalidShiftAssignment,
                "Nhân viên thay thế không thỏa mãn định mức hoặc yêu cầu kinh nghiệm ca làm việc."));

        return (true, candidate, null);
    }

    private async Task<ServiceResult> ExecuteReplacementMutationAsync(
        WorkSchedule schedule, ShiftAssignment target, LeaveRequest leave, Employee candidate,
        int managerAccountId, CancellationToken cancellationToken)
    {
        var oldEmployeeId = target.EmployeeId;
        var oldSource = target.Source;
        var now = _clock.UtcNow;

        // Thay thế có kiểm soát: chỉ đổi nhân viên trên phân công bị ảnh hưởng,
        // không tự tạo hoặc xóa ca ngoài lựa chọn của Quản lý.
        target.EmployeeId = candidate.EmployeeId;
        target.Source = AssignmentSource.Manual;
        target.UpdatedAt = now;
        schedule.UpdatedAt = now;

        var auditOld = new
        {
            leave.LeaveRequestId,
            target.ShiftAssignmentId,
            schedule.SchedulePeriodId,
            target.WorkDate,
            target.ShiftTypeId,
            OldEmployeeId = oldEmployeeId,
            OldSource = oldSource
        };
        var auditNew = new
        {
            leave.LeaveRequestId,
            target.ShiftAssignmentId,
            schedule.SchedulePeriodId,
            target.WorkDate,
            target.ShiftTypeId,
            NewEmployeeId = candidate.EmployeeId,
            NewSource = AssignmentSource.Manual
        };

        _auditLogRepository.Add(AuditLogFactory.Create(
            managerAccountId, "REPLACE_APPROVED_LEAVE_ASSIGNMENT", nameof(ShiftAssignment),
            target.ShiftAssignmentId.ToString(), auditOld, auditNew, now));

        var oldEmployee = await _employeeRepository.GetByIdAsync(oldEmployeeId, true, cancellationToken);
        AddReplacementNotifications(schedule.SchedulePeriodId, target, oldEmployee, candidate, now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Replaced assignment {AssignmentId}. Manager: {ManagerId}; Old: {OldId}; New: {NewId}",
            target.ShiftAssignmentId, managerAccountId, oldEmployeeId, candidate.EmployeeId);

        await TrySendReplacementEmailsAsync(oldEmployee, candidate, target.WorkDate, target.ShiftType?.ShiftName,
            schedule.SchedulePeriodId, cancellationToken);
        return ServiceResult.Success();
    }

    private void AddReplacementNotifications(
        int periodId, ShiftAssignment assignment, Employee? oldEmployee, Employee candidate, DateTime now)
    {
        var shiftName = assignment.ShiftType?.ShiftName ?? $"Ca {assignment.ShiftTypeId}";
        var dateText = assignment.WorkDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        if (oldEmployee?.UserAccountId.HasValue == true)
        {
            _notificationRepository.Add(new Notification
            {
                ReceiverAccountId = oldEmployee.UserAccountId.Value,
                Title = "Điều chỉnh lịch làm việc",
                Content = $"Ca {shiftName} ngày {dateText} của bạn đã được Quản lý bố trí người thay thế.",
                Type = NotificationType.ScheduleChanged,
                RelatedObjectId = periodId,
                CreatedAt = now
            });
        }
        if (candidate.UserAccountId.HasValue)
        {
            _notificationRepository.Add(new Notification
            {
                ReceiverAccountId = candidate.UserAccountId.Value,
                Title = "Phân công lịch làm việc mới",
                Content = $"Bạn được phân công làm việc ca {shiftName} ngày {dateText}.",
                Type = NotificationType.ScheduleChanged,
                RelatedObjectId = periodId,
                CreatedAt = now
            });
        }
    }

    private async Task TrySendReplacementEmailsAsync(
        Employee? oldEmployee, Employee candidate, DateOnly workDate, string? shiftName,
        int schedulePeriodId, CancellationToken cancellationToken)
    {
        if (_emailSender is null) return;
        var shiftText = string.IsNullOrWhiteSpace(shiftName) ? "ca làm việc" : $"ca {shiftName}";
        var dateText = workDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var subject = "Thông báo điều chỉnh lịch làm việc";
        var recipients = new List<(string Email, string Body)>();

        if (!string.IsNullOrWhiteSpace(oldEmployee?.Email))
        {
            recipients.Add((oldEmployee.Email.Trim(),
                $"Chào {oldEmployee.FullName}, {shiftText} ngày {dateText} của bạn đã được bố trí người thay."));
        }
        if (!string.IsNullOrWhiteSpace(candidate.Email) && !recipients.Any(
            r => r.Email.Equals(candidate.Email.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            recipients.Add((candidate.Email.Trim(),
                $"Chào {candidate.FullName}, bạn vừa được phân công làm việc {shiftText} ngày {dateText}."));
        }

        foreach (var (email, body) in recipients)
        {
            try { await _emailSender.SendAsync(email, subject, body, cancellationToken); }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex, "Failed to send replacement email to {Email} for Period {PeriodId}",
                    email, schedulePeriodId);
            }
        }
    }

    private bool HasShiftEnded(DateOnly workDate, ShiftType shiftType)
    {
        var endDate = shiftType.EndTime <= shiftType.StartTime ? workDate.AddDays(1) : workDate;
        var shiftEndVietnam = endDate.ToDateTime(shiftType.EndTime);
        return _clock.ToVietnamTime(_clock.UtcNow) >= shiftEndVietnam;
    }
}
