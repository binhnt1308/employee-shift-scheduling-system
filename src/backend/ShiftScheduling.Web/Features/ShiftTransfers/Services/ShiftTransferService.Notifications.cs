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
        var resultText = request.ApprovalStatus == ApprovalStatus.Approved
            ? "đã được duyệt"
            : "đã bị từ chối";
        foreach (var accountId in GetResultReceiverAccountIds(request))
        {
            AddNotification(
                accountId,
                "Kết quả yêu cầu đổi ca",
                $"Yêu cầu đổi ca {resultText}.",
                NotificationType.ShiftTransferResult,
                request.ShiftTransferRequestId);
        }

        await TrySaveNotificationsAsync("manager review", request.ShiftTransferRequestId, cancellationToken);
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
