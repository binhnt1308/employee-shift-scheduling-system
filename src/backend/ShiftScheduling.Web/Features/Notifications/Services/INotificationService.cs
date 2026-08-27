using ShiftScheduling.Features.Notifications.ViewModels;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.Notifications.Services;

public interface INotificationService
{
    Task<NotificationIndexViewModel> GetMyNotificationsAsync(
        int accountId,
        int page,
        CancellationToken cancellationToken = default);

    Task<int> GetMyUnreadCountAsync(
        int accountId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> MarkAsReadAsync(
        int notificationId,
        int accountId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> MarkAllAsReadAsync(
        int accountId,
        CancellationToken cancellationToken = default);
}
