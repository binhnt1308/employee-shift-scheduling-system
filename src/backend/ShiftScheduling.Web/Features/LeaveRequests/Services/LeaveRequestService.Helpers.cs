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

        var shifts = await GetFixedShiftTypesAsync(cancellationToken);
        var shift = model.ShiftTypeId.HasValue
            ? shifts.SingleOrDefault(item => item.ShiftTypeId == model.ShiftTypeId.Value)
            : null;
        if (model.ShiftTypeId.HasValue && shift is null)
        {
            return ServiceResult.Failure(ErrorCodes.ShiftTypeNotFound, "Ca xin nghỉ không hợp lệ.");
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
        IReadOnlyList<LeaveRequest> requests,
        CancellationToken cancellationToken)
    {
        var result = new List<LeaveRequestListItemViewModel>(requests.Count);
        foreach (var request in requests)
        {
            var affected = await HasPublishedAssignmentAsync(request, cancellationToken);
            result.Add(new LeaveRequestListItemViewModel
            {
                LeaveRequestId = request.LeaveRequestId,
                EmployeeCode = request.Employee.EmployeeCode,
                EmployeeName = request.Employee.FullName,
                PeriodName = request.SchedulePeriod.PeriodName,
                LeaveDate = request.LeaveDate,
                ShiftName = request.ShiftType?.ShiftName ?? "Cả ngày",
                Reason = request.Reason,
                Status = request.Status,
                SubmittedAt = request.SubmittedAt,
                HasPublishedAssignment = affected,
                RowVersion = Convert.ToBase64String(request.RowVersion)
            });
        }

        return result;
    }

    private async Task<LeaveRequestDetailsViewModel> MapDetailsAsync(
        LeaveRequest request,
        bool isManager,
        CancellationToken cancellationToken)
    {
        return new LeaveRequestDetailsViewModel
        {
            LeaveRequestId = request.LeaveRequestId,
            EmployeeCode = request.Employee.EmployeeCode,
            EmployeeName = request.Employee.FullName,
            PeriodName = request.SchedulePeriod.PeriodName,
            LeaveDate = request.LeaveDate,
            ShiftName = request.ShiftType?.ShiftName ?? "Cả ngày",
            Reason = request.Reason,
            Status = request.Status,
            SubmittedAt = request.SubmittedAt,
            ReviewedAt = request.ReviewedAt,
            ManagerNote = request.ManagerNote,
            HasPublishedAssignment = await HasPublishedAssignmentAsync(request, cancellationToken),
            HasDraftAssignment = isManager
                && await _leaveRequestRepository.HasDraftAssignmentAsync(
                    request.EmployeeId,
                    request.SchedulePeriodId,
                    request.LeaveDate,
                    request.ShiftTypeId,
                    cancellationToken),
            CanReview = isManager && request.Status == LeaveRequestStatus.Pending,
            RowVersion = Convert.ToBase64String(request.RowVersion)
        };
    }

    private Task<bool> HasPublishedAssignmentAsync(
        LeaveRequest request,
        CancellationToken cancellationToken)
    {
        return _leaveRequestRepository.HasPublishedAssignmentAsync(
            request.EmployeeId,
            request.SchedulePeriodId,
            request.LeaveDate,
            request.ShiftTypeId,
            cancellationToken);
    }

    private async Task<IReadOnlyList<ShiftType>> GetFixedShiftTypesAsync(CancellationToken cancellationToken)
    {
        var shifts = await _leaveRequestRepository.GetActiveShiftTypesAsync(cancellationToken);
        return shifts
            .Where(shift => ShiftTypeConstants.IsValidFixedShiftTime(
                shift.ShiftCode,
                shift.StartTime,
                shift.EndTime))
            .OrderBy(shift => shift.DisplayOrder)
            .ToArray();
    }

    private Task<Employee?> GetCurrentEmployeeAsync(
        int actorAccountId,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        return _leaveRequestRepository.GetEmployeeByAccountIdAsync(
            actorAccountId,
            asNoTracking,
            cancellationToken);
    }

    private LeaveRequest BuildLeaveRequest(int employeeId, LeaveRequestCreateViewModel model)
    {
        return new LeaveRequest
        {
            EmployeeId = employeeId,
            SchedulePeriodId = model.SchedulePeriodId,
            LeaveDate = model.LeaveDate,
            ShiftTypeId = model.ShiftTypeId,
            Reason = model.Reason.Trim(),
            Status = LeaveRequestStatus.Pending,
            SubmittedAt = _clock.UtcNow
        };
    }

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

    private static object BuildAuditValues(LeaveRequest request)
    {
        return new
        {
            request.LeaveRequestId,
            request.EmployeeId,
            request.SchedulePeriodId,
            request.LeaveDate,
            request.ShiftTypeId,
            request.Status,
            request.ApprovedByAccountId,
            request.ReviewedAt
        };
    }

    private static LeavePeriodOptionViewModel MapPeriodOption(SchedulePeriod period)
    {
        return new LeavePeriodOptionViewModel(
            period.SchedulePeriodId,
            period.PeriodName,
            period.WeekStartDate,
            period.WeekEndDate);
    }

    private static LeaveShiftOptionViewModel MapShiftOption(ShiftType shift)
    {
        return new LeaveShiftOptionViewModel(
            shift.ShiftTypeId,
            shift.ShiftName,
            shift.StartTime,
            shift.EndTime);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
