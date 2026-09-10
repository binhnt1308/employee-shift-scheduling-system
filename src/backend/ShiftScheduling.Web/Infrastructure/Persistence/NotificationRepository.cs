using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Common.Notifications;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.ViewModels;

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

    public async Task<IReadOnlyList<int>> GetActiveEmployeeAccountIdsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserAccounts
            .AsNoTracking()
            .Where(account => !account.IsLocked &&
                              account.Role == UserRole.Employee &&
                              account.Employee != null &&
                              account.Employee.IsActive)
            .Select(account => account.UserAccountId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Notification>> GetPagedByReceiverAccountIdAsync(
        int receiverAccountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.ReceiverAccountId == receiverAccountId);

        var totalItems = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.NotificationId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Notification>(items, page, pageSize, totalItems);
    }

    public async Task<int> GetUnreadCountByReceiverAccountIdAsync(
        int receiverAccountId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.ReceiverAccountId == receiverAccountId && !n.IsRead)
            .CountAsync(cancellationToken);
    }

    public async Task<Notification?> GetByIdAndReceiverAccountIdAsync(
        int notificationId,
        int receiverAccountId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications
            .FirstOrDefaultAsync(
                n => n.NotificationId == notificationId && n.ReceiverAccountId == receiverAccountId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> GetUnreadByReceiverAccountIdAsync(
        int receiverAccountId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications
            .Where(n => n.ReceiverAccountId == receiverAccountId && !n.IsRead)
            .ToListAsync(cancellationToken);
    }
}
