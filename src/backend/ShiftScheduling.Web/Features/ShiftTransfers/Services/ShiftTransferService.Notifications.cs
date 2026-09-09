using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.ShiftTransfers.Services;

public sealed partial class ShiftTransferService
{
    private async Task TrySaveRequestNotificationAsync(
        ShiftTransferRequest request,
        CancellationToken cancellationToken)
    {
        var receiver = await _repository.GetRequestByIdAsync(
            request.ShiftTransferRequestId,
            true,
            cancellationToken);
        var receiverAccountId = receiver?.ReceiverEmployee.UserAccountId;
        if (receiverAccountId.HasValue)
        {
            AddNotification(
                receiverAccountId.Value,
                "Yêu cầu nhận ca",
                "Bạn có một yêu cầu nhận ca mới cần phản hồi.",
                NotificationType.ShiftTransferRequest,
                request.ShiftTransferRequestId);
        }

        await TrySaveNotificationsAsync("submit", request.ShiftTransferRequestId, cancellationToken);
    }

    private async Task NotifyAfterReceiverResponseAsync(
        ShiftTransferRequest request,
        bool accepted,
        CancellationToken cancellationToken)
    {
        if (accepted)
        {
            var managerIds = await _notificationRepository.GetActiveManagerAccountIdsAsync(cancellationToken);
            foreach (var managerId in managerIds)
            {
                AddNotification(
                    managerId,
                    "Yêu cầu đổi ca chờ duyệt",
                    "Người nhận đã đồng ý nhận ca. Yêu cầu đang chờ Quản lý xử lý.",
                    NotificationType.ShiftTransferRequest,
                    request.ShiftTransferRequestId);
            }
        }
        else if (request.SenderEmployee.UserAccountId.HasValue)
        {
            AddNotification(
                request.SenderEmployee.UserAccountId.Value,
                "Kết quả yêu cầu đổi ca",
                "Người nhận đã từ chối yêu cầu nhận ca.",
                NotificationType.ShiftTransferResult,
                request.ShiftTransferRequestId);
        }

        await TrySaveNotificationsAsync("receiver response", request.ShiftTransferRequestId, cancellationToken);
    }

    private async Task NotifyTransferResultAsync(
        ShiftTransferRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ApprovalStatus == ApprovalStatus.Approved)
        {
            var periodId = request.ShiftAssignment?.WorkSchedule?.SchedulePeriodId
                ?? (request.ShiftAssignment?.WorkScheduleId > 0 ? request.ShiftAssignment.WorkScheduleId : 0);
            var title = "Lịch làm việc có thay đổi";
            var content = "Yêu cầu đổi ca đã được duyệt. Lịch làm việc chính thức của bạn đã được cập nhật.";
            foreach (var accountId in GetResultReceiverAccountIds(request))
            {
                AddNotification(
                    accountId,
                    title,
                    content,
                    NotificationType.ScheduleChanged,
                    periodId);
            }
        }
        else
        {
            var title = "Kết quả yêu cầu đổi ca";
            var content = "Yêu cầu đổi ca đã bị từ chối.";
            foreach (var accountId in GetResultReceiverAccountIds(request))
            {
                AddNotification(
                    accountId,
                    title,
                    content,
                    NotificationType.ShiftTransferResult,
                    request.ShiftTransferRequestId);
            }
        }

        await TrySaveNotificationsAsync("manager review", request.ShiftTransferRequestId, cancellationToken);
    }

    private async Task TrySendTransferEmailsAsync(
        ShiftTransferRequest request,
        CancellationToken cancellationToken)
    {
        if (_emailSender is null)
        {
            return;
        }

        string subject;
        string body;

        if (request.ApprovalStatus == ApprovalStatus.Approved)
        {
            subject = "Lịch làm việc đã thay đổi - Yêu cầu đổi ca đã được duyệt";
            body = "Yêu cầu đổi ca đã được Quản lý phê duyệt. Lịch làm việc chính thức của bạn đã được cập nhật. Vui lòng đăng nhập hệ thống để xem lịch làm việc mới.";
        }
        else
        {
            subject = "Kết quả yêu cầu đổi ca";
            var note = string.IsNullOrWhiteSpace(request.ManagerNote) ? "" : $" Ghi chú của quản lý: {request.ManagerNote}.";
            body = $"Yêu cầu đổi ca đã bị Quản lý từ chối.{note} Vui lòng đăng nhập hệ thống để xem chi tiết.";
        }

        var emails = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.SenderEmployee?.Email))
        {
            emails.Add(request.SenderEmployee.Email.Trim());
        }
        if (!string.IsNullOrWhiteSpace(request.ReceiverEmployee?.Email))
        {
            emails.Add(request.ReceiverEmployee.Email.Trim());
        }

        var distinctEmails = emails.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var email in distinctEmails)
        {
            try
            {
                await _emailSender.SendAsync(email, subject, body, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to send shift transfer result email to {Email}. RequestId: {RequestId}",
                    email,
                    request.ShiftTransferRequestId);
            }
        }
    }

    private IEnumerable<int> GetResultReceiverAccountIds(ShiftTransferRequest request)
    {
        if (request.SenderEmployee.UserAccountId.HasValue)
        {
            yield return request.SenderEmployee.UserAccountId.Value;
        }

        if (request.ReceiverEmployee.UserAccountId.HasValue)
        {
            yield return request.ReceiverEmployee.UserAccountId.Value;
        }
    }

    private void AddNotification(
        int receiverAccountId,
        string title,
        string content,
        NotificationType type,
        int relatedObjectId)
    {
        _notificationRepository.Add(new Notification
        {
            ReceiverAccountId = receiverAccountId,
            Title = title,
            Content = content,
            Type = type,
            RelatedObjectId = relatedObjectId,
            IsRead = false,
            CreatedAt = _clock.UtcNow
        });
    }

    private async Task TrySaveNotificationsAsync(
        string stage,
        int requestId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Shift transfer state was saved but notification failed. Stage: {Stage}; RequestId: {RequestId}",
                stage,
                requestId);
        }
    }
}
