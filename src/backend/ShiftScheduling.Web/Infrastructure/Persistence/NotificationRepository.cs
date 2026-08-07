using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Common.Notifications;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Infrastructure.Persistence;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly ApplicationDbContext _dbContext;

    public NotificationRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(Notification notification)
    {
        _dbContext.Notifications.Add(notification);
    }

    public async Task<IReadOnlyList<int>> GetActiveManagerAccountIdsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserAccounts
            .AsNoTracking()
            .Where(account => !account.IsLocked && account.Role == UserRole.Manager)
            .Select(account => account.UserAccountId)
            .ToListAsync(cancellationToken);
    }
}
