using System.Globalization;
using Microsoft.Extensions.Logging;
using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Constants;
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
    public async Task<LeaveReplacementViewModel?> GetLeaveReplacementPreviewAsync(
        int leaveRequestId, CancellationToken cancellationToken = default)
    {
        var leave = await _leaveRequestRepository.GetByIdAsync(leaveRequestId, true, cancellationToken);
        if (leave is null || leave.Status != LeaveRequestStatus.Pending) return null;

        return await BuildLeaveReplacementCoreAsync(leave, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ApproveLeaveWithReplacementAsync(
        ApproveLeaveWithReplacementRequestViewModel model,
        int managerAccountId,
        CancellationToken cancellationToken = default)
    {
        var load = await LoadPendingLeaveAndScheduleAsync(model.LeaveRequestId, model.RowVersion, cancellationToken);
        if (!load.Succeeded) return load.Error!;

        var (leave, period, schedule, affectedAssignments) =
            (load.Leave!, load.Period!, load.Schedule!, load.AffectedAssignments!);

        var validateSubmissions = ValidateReplacementSubmissions(model, affectedAssignments);
        if (!validateSubmissions.Succeeded) return validateSubmissions.Result!;

        var candidateValidation = await ValidateAllReplacementCandidatesAsync(
            period, schedule, leave, affectedAssignments, model.Replacements, cancellationToken);
        if (!candidateValidation.Succeeded) return candidateValidation.Result!;

        return await ExecuteApproveLeaveWithReplacementsMutationAsync(
            period, schedule, leave, candidateValidation.Pairs!, model.ManagerNote,
            managerAccountId, cancellationToken);
    }

    private async Task<(bool Succeeded, LeaveRequest? Leave, SchedulePeriod? Period, WorkSchedule? Schedule,
        List<ShiftAssignment>? AffectedAssignments, ServiceResult? Error)> LoadPendingLeaveAndScheduleAsync(
            int leaveRequestId, string rowVersion, CancellationToken cancellationToken)
    {
        var concurrencyCheck = TryParseRowVersion(rowVersion);
        if (!concurrencyCheck.Succeeded) return (false, null, null, null, null, concurrencyCheck.Result);
        var leave = await _leaveRequestRepository.GetByIdAsync(leaveRequestId, false, cancellationToken);
        if (leave is null)
            return (false, null, null, null, null,
                ServiceResult.Failure(ErrorCodes.LeaveRequestNotFound, "Không tìm thấy đơn xin nghỉ."));
        if (leave.Status != LeaveRequestStatus.Pending)
            return (false, null, null, null, null, ServiceResult.Failure(ErrorCodes.LeaveRequestAlreadyReviewed,
                "Đơn xin nghỉ đã được xử lý hoặc trạng thái đã thay đổi."));
        if (concurrencyCheck.RowVersion!.Length > 0)
            _leaveRequestRepository.SetOriginalRowVersion(leave, concurrencyCheck.RowVersion);
        var period = await _periodRepository.GetByIdAsync(leave.SchedulePeriodId, true, true, cancellationToken);
        var schedule = await _workScheduleRepository.GetByPeriodIdAsync(
            leave.SchedulePeriodId, false, true, cancellationToken);
        if (period is null || schedule is null || schedule.Status != ScheduleStatus.Published)
            return (false, null, null, null, null, ServiceResult.Failure(ErrorCodes.InvalidScheduleStatus,
                "Chỉ có thể xử lý đơn nghỉ trên lịch đã công bố."));
        var affectedAssignments = schedule.ShiftAssignments
            .Where(a => a.EmployeeId == leave.EmployeeId && a.WorkDate == leave.LeaveDate
                && (!leave.ShiftTypeId.HasValue || a.ShiftTypeId == leave.ShiftTypeId.Value))
            .OrderBy(a => a.ShiftType?.StartTime ?? TimeOnly.MinValue).ToList();
        if (affectedAssignments.Count == 0)
            return (false, null, null, null, null, ServiceResult.Failure(ErrorCodes.InvalidShiftAssignment,
                "Không tìm thấy ca làm việc bị ảnh hưởng trên lịch đã công bố."));
        return (true, leave, period, schedule, affectedAssignments, null);
    }

    private static (bool Succeeded, byte[]? RowVersion, ServiceResult? Result) TryParseRowVersion(string rowVersion)
    {
        try
        {
            var bytes = string.IsNullOrWhiteSpace(rowVersion)
                ? Array.Empty<byte>() : Convert.FromBase64String(rowVersion);
            return (true, bytes, null);
        }
        catch (FormatException)
        {
            return (false, null, ServiceResult.Failure(ErrorCodes.ConcurrencyConflict,
                "Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại trang."));
        }
    }

    private static (bool Succeeded, ServiceResult? Result) ValidateReplacementSubmissions(
        ApproveLeaveWithReplacementRequestViewModel model, List<ShiftAssignment> affectedAssignments)
    {
        if (model.Replacements.Count != affectedAssignments.Count)
            return (false, ServiceResult.Failure(ErrorCodes.InvalidShiftAssignment,
                "Cần chọn người thay thế cho tất cả các ca bị ảnh hưởng trước khi duyệt đơn."));

        var submittedIds = model.Replacements.Select(r => r.ShiftAssignmentId).ToHashSet();
        if (!affectedAssignments.All(a => submittedIds.Contains(a.ShiftAssignmentId)))
            return (false, ServiceResult.Failure(ErrorCodes.InvalidShiftAssignment,
                "Danh sách ca thay thế không khớp với các ca của đơn nghỉ."));

        return (true, null);
    }

    private async Task<(bool Succeeded, List<(ShiftAssignment Target, Employee Candidate)>? Pairs,
        ServiceResult? Result)> ValidateAllReplacementCandidatesAsync(
            SchedulePeriod period, WorkSchedule schedule, LeaveRequest leave,
            List<ShiftAssignment> affectedAssignments, List<ShiftReplacementItemViewModel> replacements,
            CancellationToken cancellationToken)
    {
        var submittedMap = replacements.ToDictionary(r => r.ShiftAssignmentId, r => r.CandidateEmployeeId);
        var candidatePairs = new List<(ShiftAssignment Target, Employee Candidate)>(affectedAssignments.Count);

        foreach (var assignment in affectedAssignments)
        {
            if (assignment.ActualWorkedHours.HasValue || !string.IsNullOrEmpty(assignment.WorkdayAdjustmentReason))
                return (false, null, ServiceResult.Failure(ErrorCodes.InvalidShiftAssignment,
                    "Ca làm việc đã có dữ liệu điều chỉnh giờ công, không thể thay thế nhân sự."));

            var shiftType = assignment.ShiftType
                ?? await _shiftTypeRepository.GetByIdAsync(assignment.ShiftTypeId, true, cancellationToken);
            if (shiftType is null)
                return (false, null, ServiceResult.Failure(ErrorCodes.ShiftTypeNotFound,
                    "Không tìm thấy thông tin ca."));

            assignment.ShiftType = shiftType;
            if (HasShiftEnded(assignment.WorkDate, shiftType))
                return (false, null, ServiceResult.Failure(ErrorCodes.LeaveRequestShiftEnded,
                    $"Ca làm việc {shiftType.ShiftName} đã kết thúc, không thể xếp người thay."));

            var candidateId = submittedMap[assignment.ShiftAssignmentId];
            var candidateCheck = await ValidateCandidateForReplacementAsync(
                period, schedule, assignment, leave.Employee, candidateId, cancellationToken);
            if (!candidateCheck.Succeeded) return (false, null, candidateCheck.Result);

            candidatePairs.Add((assignment, candidateCheck.Candidate!));
        }

        var allShifts = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);
        var activeEmployees = await _employeeRepository.GetAllActiveAsync(cancellationToken);
        var shiftTypeMap = allShifts.ToDictionary(s => s.ShiftTypeId);
        var submittedIds = submittedMap.Keys.ToHashSet();

        foreach (var group in candidatePairs.GroupBy(p => p.Candidate.EmployeeId))
        {
            var candidate = group.First().Candidate;
            var projectedTuples = schedule.ShiftAssignments
                .Where(a => a.EmployeeId == candidate.EmployeeId && !submittedIds.Contains(a.ShiftAssignmentId))
                .Select(a => (a.EmployeeId, a.WorkDate, a.ShiftTypeId, a.Source))
                .Concat(group.Select(g => (g.Candidate.EmployeeId, g.Target.WorkDate,
                    g.Target.ShiftTypeId, AssignmentSource.Manual)))
                .ToList();

            var (rulesOk, rulesResult) = ValidateCandidateRules(period, candidate, projectedTuples, allShifts);
            if (!rulesOk) return (false, null, rulesResult);
        }

        foreach (var (target, candidate) in candidatePairs)
        {
            var staffingOk = EvaluateShiftStaffingComposition(
                period, schedule, target, candidate, shiftTypeMap, activeEmployees);
            if (!staffingOk)
                return (false, null, ServiceResult.Failure(ErrorCodes.InvalidShiftAssignment,
                    $"Nhân viên {candidate.FullName} không thỏa mãn định mức ca {target.ShiftType?.ShiftName}."));
        }

        return (true, candidatePairs, null);
    }

    private async Task<ServiceResult> ExecuteApproveLeaveWithReplacementsMutationAsync(
        SchedulePeriod period, WorkSchedule schedule, LeaveRequest leave,
        List<(ShiftAssignment Target, Employee Candidate)> candidatePairs,
        string? managerNote, int managerAccountId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var shiftSummaries = new List<string>(candidatePairs.Count);

        foreach (var (target, candidate) in candidatePairs)
        {
            var oldEmpId = target.EmployeeId;
            var oldSource = target.Source;

            target.EmployeeId = candidate.EmployeeId;
            target.Source = AssignmentSource.Manual;
            target.UpdatedAt = now;

            var auditOld = new { leave.LeaveRequestId, target.ShiftAssignmentId, schedule.SchedulePeriodId,
                target.WorkDate, target.ShiftTypeId, OldEmployeeId = oldEmpId, OldSource = oldSource };
            var auditNew = new { leave.LeaveRequestId, target.ShiftAssignmentId, schedule.SchedulePeriodId,
                target.WorkDate, target.ShiftTypeId, NewEmployeeId = candidate.EmployeeId,
                NewSource = AssignmentSource.Manual };

            _auditLogRepository.Add(AuditLogFactory.Create(
                managerAccountId, "REPLACE_APPROVED_LEAVE_ASSIGNMENT", nameof(ShiftAssignment),
                target.ShiftAssignmentId.ToString(), auditOld, auditNew, now));

            var shiftName = target.ShiftType?.ShiftName ?? $"Ca {target.ShiftTypeId}";
            var shiftLabel = shiftName.StartsWith("Ca", StringComparison.OrdinalIgnoreCase)
                ? shiftName : $"Ca {shiftName}";
            var dateText = target.WorkDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            shiftSummaries.Add($"{shiftLabel} ngày {dateText} đã được bố trí cho " +
                $"{candidate.FullName} ({candidate.EmployeeCode})");
        }

        AddReplacementAndLeaveNotifications(schedule, leave, candidatePairs, now);

        var prevLeaveValues = new { leave.LeaveRequestId, leave.Status, leave.ApprovedByAccountId,
            leave.ReviewedAt, leave.ManagerNote };
        leave.Status = LeaveRequestStatus.Approved;
        leave.ApprovedByAccountId = managerAccountId;
        leave.ReviewedAt = now;
        leave.ManagerNote = NormalizeOptionalText(managerNote);

        var newLeaveValues = new { leave.LeaveRequestId, Status = LeaveRequestStatus.Approved,
            ApprovedByAccountId = managerAccountId, ReviewedAt = now, leave.ManagerNote };
        _auditLogRepository.Add(AuditLogFactory.Create(
            managerAccountId, "REVIEW_LEAVE_REQUEST", nameof(LeaveRequest),
            leave.LeaveRequestId.ToString(), prevLeaveValues, newLeaveValues, now));

        schedule.UpdatedAt = now;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await TrySendApprovalEmailsAsync(leave, candidatePairs, schedule.SchedulePeriodId, cancellationToken);

        var successMsg = $"Đã duyệt đơn nghỉ của {leave.Employee.FullName} ({leave.Employee.EmployeeCode}). " +
            $"{string.Join(". ", shiftSummaries)}.";
        return ServiceResult.Success(successMsg);
    }

    private void AddReplacementAndLeaveNotifications(
        WorkSchedule schedule, LeaveRequest leave,
        List<(ShiftAssignment Target, Employee Candidate)> candidatePairs, DateTime now)
    {
        var dateText = leave.LeaveDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        foreach (var group in candidatePairs.GroupBy(p => p.Candidate.EmployeeId))
        {
            var candidate = group.First().Candidate;
            if (!candidate.UserAccountId.HasValue) continue;

            var shiftLabels = string.Join(", ", group.Select(g =>
            {
                var name = g.Target.ShiftType?.ShiftName ?? $"Ca {g.Target.ShiftTypeId}";
                return name.StartsWith("Ca", StringComparison.OrdinalIgnoreCase) ? name : $"ca {name}";
            }));

            _notificationRepository.Add(new Notification
            {
                ReceiverAccountId = candidate.UserAccountId.Value,
                Title = "Phân công lịch làm việc mới",
                Content = $"Bạn được phân công làm việc {shiftLabels} ngày {dateText}.",
                Type = NotificationType.ScheduleChanged,
                RelatedObjectId = schedule.SchedulePeriodId,
                CreatedAt = now
            });
        }

        if (leave.Employee.UserAccountId.HasValue)
        {
            _notificationRepository.Add(new Notification
            {
                ReceiverAccountId = leave.Employee.UserAccountId.Value,
                Title = "Kết quả đơn xin nghỉ",
                Content = $"Đơn xin nghỉ ngày {dateText} đã được duyệt.",
                Type = NotificationType.LeaveRequestResult,
                RelatedObjectId = leave.LeaveRequestId,
                IsRead = false,
                CreatedAt = now
            });
        }
    }

    private async Task TrySendApprovalEmailsAsync(
        LeaveRequest leave, List<(ShiftAssignment Target, Employee Candidate)> candidatePairs,
        int schedulePeriodId, CancellationToken cancellationToken)
    {
        if (_emailSender is null) return;
        try
        {
            var dateText = leave.LeaveDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(leave.Employee.Email))
            {
                var body = $"Đơn xin nghỉ ngày {dateText} của bạn đã được Quản lý phê duyệt.";
                await _emailSender.SendAsync(
                    leave.Employee.Email.Trim(), "Kết quả đơn xin nghỉ", body, cancellationToken);
            }

            foreach (var group in candidatePairs.GroupBy(p => p.Candidate.EmployeeId))
            {
                var candidate = group.First().Candidate;
                if (string.IsNullOrWhiteSpace(candidate.Email)) continue;

                var shiftLabels = string.Join(", ", group.Select(g =>
                {
                    var name = g.Target.ShiftType?.ShiftName ?? $"ca {g.Target.ShiftTypeId}";
                    return name.StartsWith("ca", StringComparison.OrdinalIgnoreCase) ? name : $"ca {name}";
                }));
                var body = $"Bạn được phân công thay {shiftLabels} ngày {dateText} trên lịch đã công bố.";
                await _emailSender.SendAsync(
                    candidate.Email.Trim(), "Thông báo điều chỉnh lịch làm việc", body, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send emails for LeaveRequestId: {Id}", leave.LeaveRequestId);
        }
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <inheritdoc />
    public async Task<ServiceResult> ApproveEmergencyLeaveAsync(
        ApproveEmergencyLeaveRequestViewModel model,
        int managerAccountId,
        CancellationToken cancellationToken = default)
    {
        var load = await LoadPendingLeaveAndScheduleAsync(model.LeaveRequestId, model.RowVersion, cancellationToken);
        if (!load.Succeeded) return load.Error!;

        var (leave, affectedAssignments) = (load.Leave!, load.AffectedAssignments!);
        foreach (var assignment in affectedAssignments)
        {
            var shiftType = assignment.ShiftType
                ?? await _shiftTypeRepository.GetByIdAsync(assignment.ShiftTypeId, true, cancellationToken);
            if (shiftType is null)
                return ServiceResult.Failure(ErrorCodes.ShiftTypeNotFound, "Không tìm thấy thông tin ca.");
            assignment.ShiftType = shiftType;

            if (HasShiftEnded(assignment.WorkDate, shiftType))
                return ServiceResult.Failure(ErrorCodes.LeaveRequestShiftEnded,
                    $"Ca làm việc {shiftType.ShiftName} đã kết thúc, không thể duyệt nghỉ khẩn cấp.");
        }

        var preview = await BuildLeaveReplacementCoreAsync(leave, cancellationToken);
        var allCoverable = preview is not null && preview.AffectedAssignments.Count > 0
            && preview.AffectedAssignments.All(a => !a.IsEnded && a.Candidates.Count > 0);
        if (allCoverable)
            return ServiceResult.Failure(ErrorCodes.InvalidShiftAssignment,
                "Tất cả các ca đều có nhân viên thay thế phù hợp. Vui lòng sử dụng chức năng Bố trí người thay.");

        var now = _clock.UtcNow;
        var prevLeaveValues = new { leave.LeaveRequestId, leave.Status, leave.ApprovedByAccountId,
            leave.ReviewedAt, leave.ManagerNote };
        leave.Status = LeaveRequestStatus.Approved;
        leave.ApprovedByAccountId = managerAccountId;
        leave.ReviewedAt = now;
        leave.ManagerNote = NormalizeOptionalText(model.ManagerNote);

        var newLeaveValues = new { leave.LeaveRequestId, Status = LeaveRequestStatus.Approved,
            ApprovedByAccountId = managerAccountId, ReviewedAt = now, leave.ManagerNote };
        _auditLogRepository.Add(AuditLogFactory.Create(
            managerAccountId, "APPROVE_EMERGENCY_LEAVE", nameof(LeaveRequest),
            leave.LeaveRequestId.ToString(), prevLeaveValues, newLeaveValues, now));

        if (leave.Employee.UserAccountId.HasValue)
        {
            var dateText = leave.LeaveDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            _notificationRepository.Add(new Notification
            {
                ReceiverAccountId = leave.Employee.UserAccountId.Value, Title = "Kết quả đơn xin nghỉ",
                Content = $"Đơn xin nghỉ ngày {dateText} đã được duyệt khẩn cấp.",
                Type = NotificationType.LeaveRequestResult, RelatedObjectId = leave.LeaveRequestId,
                IsRead = false, CreatedAt = now
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_emailSender is not null && !string.IsNullOrWhiteSpace(leave.Employee.Email))
        {
            try
            {
                var dateText = leave.LeaveDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                var body = $"Đơn xin nghỉ ngày {dateText} của bạn đã được Quản lý phê duyệt khẩn cấp.";
                await _emailSender.SendAsync(
                    leave.Employee.Email.Trim(), "Kết quả đơn xin nghỉ", body, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send emergency approval email for LeaveRequestId: {Id}",
                    leave.LeaveRequestId);
            }
        }

        var successMsg = $"Đã duyệt nghỉ khẩn cấp cho {leave.Employee.FullName} ({leave.Employee.EmployeeCode}). " +
            "Quản lý sẽ hỗ trợ ca này; giờ làm của Quản lý không được ghi nhận trong hệ thống.";
        return ServiceResult.Success(successMsg);
    }
}
