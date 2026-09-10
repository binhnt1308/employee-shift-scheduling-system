using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Common.Notifications;

public interface INotificationRepository
{
    void Add(Notification notification);

    Task<IReadOnlyList<int>> GetActiveManagerAccountIdsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetActiveEmployeeAccountIdsAsync(
        CancellationToken cancellationToken = default);

    Task<PagedResult<Notification>> GetPagedByReceiverAccountIdAsync(
        int receiverAccountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountByReceiverAccountIdAsync(
        int receiverAccountId,
        CancellationToken cancellationToken = default);

    Task<Notification?> GetByIdAndReceiverAccountIdAsync(
        int notificationId,
        int receiverAccountId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notification>> GetUnreadByReceiverAccountIdAsync(
        int receiverAccountId,
        CancellationToken cancellationToken = default);
}
