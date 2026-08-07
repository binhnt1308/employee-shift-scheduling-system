using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Common.Notifications;

public interface INotificationRepository
{
    void Add(Notification notification);

    Task<IReadOnlyList<int>> GetActiveManagerAccountIdsAsync(
        CancellationToken cancellationToken = default);
}
