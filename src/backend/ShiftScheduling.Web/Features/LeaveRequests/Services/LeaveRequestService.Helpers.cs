using System.Globalization;
using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Features.LeaveRequests.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.LeaveRequests.Services;

public sealed partial class LeaveRequestService
{
    // Xác thực đơn nghỉ trên lịch đã công bố: đúng kỳ, đúng ngày/ca, ca chưa kết thúc
    // và không có đơn trùng đang còn hiệu lực.
    private async Task<ServiceResult> ValidateSubmissionAsync(
        Employee employee,
        LeaveRequestCreateViewModel model,
        CancellationToken cancellationToken)
    {
        var periods = await _leaveRequestRepository.GetPeriodsAsync(cancellationToken);
        var period = periods.SingleOrDefault(item => item.SchedulePeriodId == model.SchedulePeriodId);
        if (period is null || model.LeaveDate < period.WeekStartDate || model.LeaveDate > period.WeekEndDate)
        {
            return ServiceResult.Failure(
                ErrorCodes.LeaveRequestInvalidDate,
                "Ngày xin nghỉ phải thuộc kỳ lịch đã chọn.");
        }

        // B & D: Nhân viên phải có ít nhất một phân công đúng ngày xin nghỉ
        // trên lịch đã công bố của chính kỳ lịch đang gửi đơn.
        var hasDateAssignment = await _leaveRequestRepository.HasPublishedAssignmentAsync(
            employee.EmployeeId,
            model.SchedulePeriodId,
            model.LeaveDate,
            null,
            cancellationToken);

        if (!hasDateAssignment)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidShiftAssignment,
                "Bạn không có ca làm việc nào trong lịch chính thức vào ngày đã chọn.");
        }

        // C: Nếu chọn ca cụ thể (ShiftTypeId != null), phải tồn tại ShiftAssignment đúng ca đó
        ShiftType? shift = null;
        if (model.ShiftTypeId.HasValue)
        {
            var hasShiftAssignment = await _leaveRequestRepository.HasPublishedAssignmentAsync(
                employee.EmployeeId,
                model.SchedulePeriodId,
                model.LeaveDate,
                model.ShiftTypeId,
                cancellationToken);

            if (!hasShiftAssignment)
            {
                return ServiceResult.Failure(
                    ErrorCodes.InvalidShiftAssignment,
                    "Bạn không được phân công ca làm việc này trong lịch chính thức.");
            }

            var shifts = await GetFixedShiftTypesAsync(cancellationToken);
            shift = shifts.SingleOrDefault(item => item.ShiftTypeId == model.ShiftTypeId.Value);
            if (shift is null)
            {
                var assignments = await _leaveRequestRepository.GetEmployeePublishedAssignmentsAsync(
                    employee.EmployeeId,
                    model.LeaveDate,
                    cancellationToken);
                shift = assignments
                    .FirstOrDefault(a => a.WorkSchedule.SchedulePeriodId == model.SchedulePeriodId
                        && a.WorkDate == model.LeaveDate
                        && a.ShiftTypeId == model.ShiftTypeId.Value)
                    ?.ShiftType;
            }

            if (shift is null)
            {
                return ServiceResult.Failure(ErrorCodes.ShiftTypeNotFound, "Ca xin nghỉ không hợp lệ.");
            }
        }

        var timeValidation = ValidateNotEnded(model.LeaveDate, shift);
        if (!timeValidation.Succeeded)
        {
            return timeValidation;
        }

        // UC09: Không cho phép nhiều đơn còn hiệu lực cùng ngày/ca của một nhân viên.
        var duplicate = await _leaveRequestRepository.HasEffectiveDuplicateAsync(
            employee.EmployeeId,
            model.SchedulePeriodId,
            model.LeaveDate,
            model.ShiftTypeId,
            cancellationToken);
        return duplicate
            ? ServiceResult.Failure(
                ErrorCodes.LeaveRequestDuplicate,
                "Đã có đơn xin nghỉ còn hiệu lực cho ngày hoặc ca này.")
            : ServiceResult.Success();
    }

    private ServiceResult ValidateNotEnded(DateOnly leaveDate, ShiftType? shift)
    {
        if (leaveDate < _clock.TodayInVietnam)
        {
            return ServiceResult.Failure(
            ErrorCodes.LeaveRequestShiftEnded,
            "Ngày hoặc ca xin nghỉ đã kết thúc.");
        }

        if (leaveDate > _clock.TodayInVietnam)
        {
            return ServiceResult.Success();
        }

        var vietnamNow = _clock.ToVietnamTime(_clock.UtcNow);
        var endTime = shift?.EndTime ?? ShiftTypeConstants.EveningEndTime;
        return TimeOnly.FromDateTime(vietnamNow) >= endTime
            ? ServiceResult.Failure(ErrorCodes.LeaveRequestShiftEnded, "Ngày hoặc ca xin nghỉ đã kết thúc.")
            : ServiceResult.Success();
    }

    private async Task<IReadOnlyList<LeaveRequestListItemViewModel>> MapListItemsAsync(
        IReadOnlyList<LeaveRequest> requests, CancellationToken cancellationToken)
    {
        var result = new List<LeaveRequestListItemViewModel>(requests.Count);
        foreach (var request in requests)
        {
            var affected = await HasPublishedAssignmentAsync(request, cancellationToken);
            result.Add(new LeaveRequestListItemViewModel
            {
                LeaveRequestId = request.LeaveRequestId, EmployeeCode = request.Employee.EmployeeCode,
                EmployeeName = request.Employee.FullName, PeriodName = request.SchedulePeriod.PeriodName,
                LeaveDate = request.LeaveDate, ShiftName = request.ShiftType?.ShiftName ?? "Cả ngày",
                Reason = request.Reason, Status = request.Status,
                SubmittedAt = _clock.ToVietnamTime(request.SubmittedAt),
                HasPublishedAssignment = affected, RowVersion = Convert.ToBase64String(request.RowVersion)
            });
        }
        return result;
    }

    private async Task<LeaveRequestDetailsViewModel> MapDetailsAsync(
        LeaveRequest request, bool isManager, CancellationToken cancellationToken)
    {
        return new LeaveRequestDetailsViewModel
        {
            LeaveRequestId = request.LeaveRequestId,
            EmployeeCode = request.Employee.EmployeeCode,
            EmployeeName = request.Employee.FullName,
            PeriodName = request.SchedulePeriod.PeriodName,
            SchedulePeriodId = request.SchedulePeriodId,
            LeaveDate = request.LeaveDate,
            ShiftName = request.ShiftType?.ShiftName ?? "Cả ngày",
            Reason = request.Reason,
            Status = request.Status,
            SubmittedAt = _clock.ToVietnamTime(request.SubmittedAt),
            ReviewedAt = request.ReviewedAt.HasValue ? _clock.ToVietnamTime(request.ReviewedAt.Value) : null,
            ManagerNote = request.ManagerNote,
            HasPublishedAssignment = await HasPublishedAssignmentAsync(request, cancellationToken),
            HasDraftAssignment = isManager && await _leaveRequestRepository.HasDraftAssignmentAsync(
                request.EmployeeId, request.SchedulePeriodId, request.LeaveDate,
                request.ShiftTypeId, cancellationToken),
            CanReview = isManager && request.Status == LeaveRequestStatus.Pending,
            CanCancel = !isManager && request.Status == LeaveRequestStatus.Pending,
            IsManager = isManager,
            RowVersion = Convert.ToBase64String(request.RowVersion)
        };
    }

    private Task<bool> HasPublishedAssignmentAsync(LeaveRequest request, CancellationToken cancellationToken) =>
        _leaveRequestRepository.HasPublishedAssignmentAsync(
            request.EmployeeId, request.SchedulePeriodId, request.LeaveDate, request.ShiftTypeId, cancellationToken);

    private async Task<IReadOnlyList<ShiftType>> GetFixedShiftTypesAsync(CancellationToken cancellationToken)
    {
        var shifts = await _leaveRequestRepository.GetActiveShiftTypesAsync(cancellationToken);
        return shifts
            .Where(shift => shift.IsActive && ShiftTypeConstants.IsFixedShiftCode(shift.ShiftCode))
            .OrderBy(shift => shift.DisplayOrder)
            .ToArray();
    }

    private Task<Employee?> GetCurrentEmployeeAsync(
        int actorAccountId, bool asNoTracking, CancellationToken cancellationToken) =>
        _leaveRequestRepository.GetEmployeeByAccountIdAsync(actorAccountId, asNoTracking, cancellationToken);

    private LeaveRequest BuildLeaveRequest(int employeeId, LeaveRequestCreateViewModel model) => new()
    {
        EmployeeId = employeeId, SchedulePeriodId = model.SchedulePeriodId, LeaveDate = model.LeaveDate,
        ShiftTypeId = model.ShiftTypeId, Reason = model.Reason.Trim(), Status = LeaveRequestStatus.Pending,
        SubmittedAt = _clock.UtcNow
    };

    // Chỉ gửi thông báo sau khi đơn đã lưu thành công; lỗi thông báo được ghi log
    // và không rollback nghiệp vụ gửi đơn.
    private async Task TryNotifyManagersOnSubmitAsync(
        Employee employee,
        LeaveRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var managerAccountIds = await _notificationRepository.GetActiveManagerAccountIdsAsync(cancellationToken);
            var submitterAccountId = employee.UserAccountId;
            var recipients = managerAccountIds
                .Where(id => !submitterAccountId.HasValue || id != submitterAccountId.Value)
                .Distinct()
                .ToList();

            if (recipients.Count == 0)
            {
                return;
            }

            string shiftDescription;
            var dateText = request.LeaveDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            if (request.ShiftTypeId.HasValue)
            {
                var shifts = await GetFixedShiftTypesAsync(cancellationToken);
                var shift = shifts.SingleOrDefault(s => s.ShiftTypeId == request.ShiftTypeId.Value);
                if (shift is null)
                {
                    var assignments = await _leaveRequestRepository.GetEmployeePublishedAssignmentsAsync(
                        employee.EmployeeId,
                        request.LeaveDate,
                        cancellationToken);
                    shift = assignments
                        .FirstOrDefault(a => a.WorkSchedule.SchedulePeriodId == request.SchedulePeriodId
                            && a.WorkDate == request.LeaveDate
                            && a.ShiftTypeId == request.ShiftTypeId.Value)
                        ?.ShiftType;
                }

                var shiftName = shift?.ShiftName ?? request.ShiftType?.ShiftName;
                if (string.IsNullOrWhiteSpace(shiftName))
                {
                    shiftDescription = $"ngày {dateText}";
                }
                else
                {
                    var normalizedShift = shiftName.StartsWith("ca ", StringComparison.OrdinalIgnoreCase)
                        ? "ca " + shiftName[3..].Trim()
                        : $"ca {shiftName.Trim()}";
                    shiftDescription = $"{normalizedShift} ngày {dateText}";
                }
            }
            else
            {
                shiftDescription = $"cả ngày {dateText}";
            }

            var content = $"{employee.EmployeeCode} - {employee.FullName} vừa gửi đơn xin nghỉ {shiftDescription}.";

            foreach (var managerAccountId in recipients)
            {
                _notificationRepository.Add(new Notification
                {
                    ReceiverAccountId = managerAccountId,
                    Title = "Đơn xin nghỉ mới",
                    Content = content,
                    Type = NotificationType.LeaveRequestSubmitted,
                    RelatedObjectId = request.LeaveRequestId,
                    IsRead = false,
                    CreatedAt = _clock.UtcNow
                });
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Leave request submitted but manager notification failed. LeaveRequestId: {LeaveRequestId}",
                request.LeaveRequestId);
        }
    }

    // Lỗi lưu thông báo hoặc gửi email cho nhân viên không được làm thay đổi kết quả duyệt đơn.
    private async Task TryNotifyEmployeeAsync(LeaveRequest request, CancellationToken cancellationToken)
    {
        var receiverAccountId = request.Employee.UserAccountId;
        if (!receiverAccountId.HasValue)
        {
            return;
        }

        var statusText = request.Status == LeaveRequestStatus.Approved ? "được duyệt" : "bị từ chối";
        _notificationRepository.Add(new Notification
        {
            ReceiverAccountId = receiverAccountId.Value,
            Title = "Kết quả đơn xin nghỉ",
            Content = $"Đơn xin nghỉ ngày {request.LeaveDate:dd/MM/yyyy} đã {statusText}.",
            Type = NotificationType.LeaveRequestResult,
            RelatedObjectId = request.LeaveRequestId,
            IsRead = false,
            CreatedAt = _clock.UtcNow
        });

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Leave request was reviewed but notification could not be saved. LeaveRequestId: {LeaveRequestId}",
                request.LeaveRequestId);
        }

        await TrySendLeaveEmailAsync(request.Employee?.Email?.Trim(), request, cancellationToken);
    }

    private async Task TrySendLeaveEmailAsync(
        string? email,
        LeaveRequest request,
        CancellationToken cancellationToken)
    {
        if (_emailSender is null || string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var isApproved = request.Status == LeaveRequestStatus.Approved;
        var subject = "Kết quả đơn xin nghỉ";
        var dateFormatted = request.LeaveDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var shiftInfo = request.ShiftType != null ? $" ca {request.ShiftType.ShiftName}" : "";

        string body;
        if (isApproved)
        {
            body = $"Đơn xin nghỉ ngày {dateFormatted}{shiftInfo} của bạn đã được Quản lý phê duyệt. " +
                "Vui lòng đăng nhập hệ thống để xem chi tiết.";
        }
        else
        {
            var note = string.IsNullOrWhiteSpace(request.ManagerNote) ? "" : $" Ghi chú: {request.ManagerNote}.";
            body = $"Đơn xin nghỉ ngày {dateFormatted}{shiftInfo} của bạn đã bị Quản lý từ chối.{note} " +
                "Vui lòng đăng nhập hệ thống để xem chi tiết.";
        }

        try
        {
            await _emailSender.SendAsync(email, subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send leave request result email to {Email}. LeaveRequestId: {LeaveRequestId}",
                email,
                request.LeaveRequestId);
        }
    }

    private void AddAuditLog(
        int actorAccountId,
        string action,
        LeaveRequest request,
        object? previousValues,
        object? newValues)
    {
        var entityId = request.LeaveRequestId == 0
            ? $"{request.EmployeeId}:{request.LeaveDate:yyyyMMdd}:{request.ShiftTypeId?.ToString() ?? "DAY"}"
            : request.LeaveRequestId.ToString();
        _auditLogRepository.Add(AuditLogFactory.Create(
            actorAccountId,
            action,
            nameof(LeaveRequest),
            entityId,
            previousValues,
            newValues,
            _clock.UtcNow));
    }

    private static object BuildAuditValues(LeaveRequest request) => new
    {
        request.LeaveRequestId, request.EmployeeId, request.SchedulePeriodId, request.LeaveDate,
        request.ShiftTypeId, request.Status, request.ApprovedByAccountId, request.ReviewedAt
    };

    private static LeavePeriodOptionViewModel MapPeriodOption(SchedulePeriod period) =>
        new(period.SchedulePeriodId, period.PeriodName, period.WeekStartDate, period.WeekEndDate);

    private static LeaveShiftOptionViewModel MapShiftOption(ShiftType shift) =>
        new(shift.ShiftTypeId, shift.ShiftName, shift.StartTime, shift.EndTime);

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
