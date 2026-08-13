using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Features.ShiftTransfers.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.ShiftTransfers.Services;

public sealed partial class ShiftTransferService
{
    private async Task<IReadOnlyList<ShiftReceiverOptionViewModel>> GetReceiverOptionsAsync(
        Employee sender,
        int shiftAssignmentId,
        CancellationToken cancellationToken)
    {
        if (shiftAssignmentId <= 0)
        {
            return [];
        }

        var assignment = await _repository.GetAssignmentByIdAsync(shiftAssignmentId, true, cancellationToken);
        if (assignment is null || assignment.EmployeeId != sender.EmployeeId || HasShiftStarted(assignment))
        {
            return [];
        }

        var candidates = await _repository.GetActiveEmployeesByPositionAsync(
            sender.Position,
            sender.EmployeeId,
            cancellationToken);
        var result = new List<ShiftReceiverOptionViewModel>();
        foreach (var candidate in candidates.Where(IsInteractiveEmployee))
        {
            var validation = await ValidateTransferAsync(assignment, sender, candidate, cancellationToken);
            if (validation.BlockingErrors.Count == 0)
            {
                result.Add(MapReceiverOption(candidate));
            }
        }

        return result;
    }

    private async Task<Employee?> GetEmployeeByIdFromCandidatesAsync(
        Employee sender,
        int receiverEmployeeId,
        CancellationToken cancellationToken)
    {
        var candidates = await _repository.GetActiveEmployeesByPositionAsync(
            sender.Position,
            sender.EmployeeId,
            cancellationToken);
        return candidates.SingleOrDefault(candidate =>
            candidate.EmployeeId == receiverEmployeeId && IsInteractiveEmployee(candidate));
    }

    private static IReadOnlyList<ShiftTransferRequest> FilterManagerRequests(
        IReadOnlyList<ShiftTransferRequest> requests,
        ShiftTransferStatusFilter? status)
    {
        if (!status.HasValue)
        {
            return requests;
        }

        return status.Value switch
        {
            ShiftTransferStatusFilter.WaitingReceiver => requests
                .Where(request => request.ReceiverStatus == ReceiverStatus.Pending
                    && request.ApprovalStatus == ApprovalStatus.Pending)
                .ToArray(),
            ShiftTransferStatusFilter.WaitingManager => requests
                .Where(request => request.ReceiverStatus == ReceiverStatus.Accepted
                    && request.ApprovalStatus == ApprovalStatus.Pending)
                .ToArray(),
            ShiftTransferStatusFilter.Approved => requests
                .Where(request => request.ApprovalStatus == ApprovalStatus.Approved)
                .ToArray(),
            ShiftTransferStatusFilter.Rejected => requests
                .Where(request => request.ReceiverStatus == ReceiverStatus.Rejected
                    || request.ApprovalStatus == ApprovalStatus.Rejected)
                .ToArray(),
            _ => requests
        };
    }

    private ServiceResult ValidateReceiverResponse(Employee? employee, ShiftTransferRequest? request)
    {
        if (employee is null || request is null || request.ReceiverEmployeeId != employee.EmployeeId)
        {
            return ServiceResult.Failure(ErrorCodes.ShiftTransferNotFound, "Không tìm thấy yêu cầu đổi ca phù hợp.");
        }

        if (request.ReceiverStatus != ReceiverStatus.Pending || request.ApprovalStatus != ApprovalStatus.Pending)
        {
            return ServiceResult.Failure(
                ErrorCodes.ShiftTransferAlreadyHandled,
                "Yêu cầu đã được phản hồi hoặc xử lý.");
        }

        return ValidateAssignmentState(request.ShiftAssignment, request.SenderEmployeeId);
    }

    private static ServiceResult ValidateManagerReviewState(ShiftTransferRequest? request)
    {
        if (request is null)
        {
            return ServiceResult.Failure(ErrorCodes.ShiftTransferNotFound, "Không tìm thấy yêu cầu đổi ca.");
        }

        if (request.ReceiverStatus != ReceiverStatus.Accepted)
        {
            return ServiceResult.Failure(ErrorCodes.ShiftTransferNotAccepted, "Người nhận chưa đồng ý nhận ca.");
        }

        return request.ApprovalStatus != ApprovalStatus.Pending
            ? ServiceResult.Failure(ErrorCodes.ShiftTransferAlreadyHandled, "Yêu cầu đã được Quản lý xử lý.")
            : ServiceResult.Success();
    }

    private void ApplyReceiverResponse(ShiftTransferRequest request, bool accept)
    {
        request.ReceiverStatus = accept ? ReceiverStatus.Accepted : ReceiverStatus.Rejected;
        request.ReceiverRespondedAt = _clock.UtcNow;
        if (!accept)
        {
            request.ApprovalStatus = ApprovalStatus.Rejected;
        }
    }

    private void ApplyManagerDecision(
        ShiftTransferRequest request,
        bool approve,
        string? managerNote,
        int managerAccountId)
    {
        request.ApprovalStatus = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        request.ApprovedByAccountId = managerAccountId;
        request.ReviewedAt = _clock.UtcNow;
        request.ManagerNote = NormalizeOptionalText(managerNote);
        if (!approve)
        {
            return;
        }

        // BR17: Cập nhật một chiều trong cùng bản ghi phân công để loại người gửi và gán người nhận đồng thời.
        request.ShiftAssignment.EmployeeId = request.ReceiverEmployeeId;
        request.ShiftAssignment.Employee = request.ReceiverEmployee;
        request.ShiftAssignment.Source = AssignmentSource.ShiftTransfer;
        request.ShiftAssignment.UpdatedAt = _clock.UtcNow;
    }

    private bool HasShiftStarted(ShiftAssignment assignment)
    {
        var today = _clock.TodayInVietnam;
        if (assignment.WorkDate < today)
        {
            return true;
        }

        if (assignment.WorkDate > today)
        {
            return false;
        }

        var vietnamNow = _clock.ToVietnamTime(_clock.UtcNow);
        return TimeOnly.FromDateTime(vietnamNow) >= assignment.ShiftType.StartTime;
    }

    private static bool IsInteractiveEmployee(Employee employee)
    {
        return employee.IsActive
            && employee.UserAccountId.HasValue
            && employee.UserAccount is { IsLocked: false };
    }

    private static ShiftTransferListItemViewModel MapListItem(
        ShiftTransferRequest request,
        bool receivedByCurrentEmployee,
        bool isManager)
    {
        return new ShiftTransferListItemViewModel
        {
            ShiftTransferRequestId = request.ShiftTransferRequestId,
            SenderName = request.SenderEmployee.FullName,
            ReceiverName = request.ReceiverEmployee.FullName,
            PeriodName = request.ShiftAssignment.WorkSchedule.SchedulePeriod.PeriodName,
            WorkDate = request.ShiftAssignment.WorkDate,
            ShiftName = request.ShiftAssignment.ShiftType.ShiftName,
            ReceiverStatus = request.ReceiverStatus,
            ApprovalStatus = request.ApprovalStatus,
            SubmittedAt = request.SubmittedAt,
            CanRespond = receivedByCurrentEmployee
                && request.ReceiverStatus == ReceiverStatus.Pending
                && request.ApprovalStatus == ApprovalStatus.Pending,
            CanReview = isManager
                && request.ReceiverStatus == ReceiverStatus.Accepted
                && request.ApprovalStatus == ApprovalStatus.Pending
        };
    }

    private static ShiftTransferDetailsViewModel MapDetails(
        ShiftTransferRequest request,
        int? currentEmployeeId,
        bool isManager)
    {
        return new ShiftTransferDetailsViewModel
        {
            ShiftTransferRequestId = request.ShiftTransferRequestId,
            SenderName = request.SenderEmployee.FullName,
            ReceiverName = request.ReceiverEmployee.FullName,
            PeriodName = request.ShiftAssignment.WorkSchedule.SchedulePeriod.PeriodName,
            WorkDate = request.ShiftAssignment.WorkDate,
            ShiftName = request.ShiftAssignment.ShiftType.ShiftName,
            StartTime = request.ShiftAssignment.ShiftType.StartTime,
            EndTime = request.ShiftAssignment.ShiftType.EndTime,
            PositionName = GetPositionName(request.SenderEmployee.Position),
            Reason = request.Reason,
            ReceiverStatus = request.ReceiverStatus,
            ApprovalStatus = request.ApprovalStatus,
            SubmittedAt = request.SubmittedAt,
            ReceiverRespondedAt = request.ReceiverRespondedAt,
            ReviewedAt = request.ReviewedAt,
            ManagerNote = request.ManagerNote,
            CanRespond = currentEmployeeId == request.ReceiverEmployeeId
                && request.ReceiverStatus == ReceiverStatus.Pending
                && request.ApprovalStatus == ApprovalStatus.Pending,
            CanReview = isManager
                && request.ReceiverStatus == ReceiverStatus.Accepted
                && request.ApprovalStatus == ApprovalStatus.Pending
        };
    }

    private static ShiftAssignmentOptionViewModel MapAssignmentOption(ShiftAssignment assignment)
    {
        return new ShiftAssignmentOptionViewModel(
            assignment.ShiftAssignmentId,
            assignment.WorkSchedule.SchedulePeriod.PeriodName,
            assignment.WorkDate,
            assignment.ShiftType.ShiftName,
            assignment.ShiftType.StartTime,
            assignment.ShiftType.EndTime);
    }

    private static ShiftReceiverOptionViewModel MapReceiverOption(Employee employee)
    {
        return new ShiftReceiverOptionViewModel(
            employee.EmployeeId,
            employee.EmployeeCode,
            employee.FullName,
            employee.Level,
            employee.EmploymentType);
    }

    private ShiftTransferRequest BuildRequest(int senderEmployeeId, ShiftTransferCreateViewModel model)
    {
        return new ShiftTransferRequest
        {
            ShiftAssignmentId = model.ShiftAssignmentId,
            SenderEmployeeId = senderEmployeeId,
            ReceiverEmployeeId = model.ReceiverEmployeeId,
            Reason = model.Reason.Trim(),
            ReceiverStatus = ReceiverStatus.Pending,
            ApprovalStatus = ApprovalStatus.Pending,
            SubmittedAt = _clock.UtcNow
        };
    }

    private Task<Employee?> GetCurrentEmployeeAsync(
        int actorAccountId,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        return _repository.GetEmployeeByAccountIdAsync(actorAccountId, asNoTracking, cancellationToken);
    }

    private void AddAuditLog(
        int actorAccountId,
        string action,
        ShiftTransferRequest request,
        object? previousValues,
        object? newValues)
    {
        var entityId = request.ShiftTransferRequestId == 0
            ? $"{request.ShiftAssignmentId}:{request.SenderEmployeeId}:{request.ReceiverEmployeeId}"
            : request.ShiftTransferRequestId.ToString();
        _auditLogRepository.Add(AuditLogFactory.Create(
            actorAccountId,
            action,
            nameof(ShiftTransferRequest),
            entityId,
            previousValues,
            newValues,
            _clock.UtcNow));
    }

    private static object BuildAuditValues(ShiftTransferRequest request)
    {
        return new
        {
            request.ShiftTransferRequestId,
            request.ShiftAssignmentId,
            request.SenderEmployeeId,
            request.ReceiverEmployeeId,
            request.ReceiverStatus,
            request.ApprovalStatus,
            request.ApprovedByAccountId,
            request.ReviewedAt
        };
    }

    private static string GetPositionName(EmployeePosition position)
    {
        return position == EmployeePosition.Sales ? "Tư vấn" : "Thu ngân";
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
