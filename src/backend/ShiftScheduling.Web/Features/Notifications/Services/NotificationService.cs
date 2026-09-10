using ShiftScheduling.Common.Notifications;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.Notifications.ViewModels;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Notifications.Services;

public sealed class NotificationService : INotificationService
{
    private const int DefaultPageSize = 20;

    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public NotificationService(
        INotificationRepository notificationRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<NotificationIndexViewModel> GetMyNotificationsAsync(
        int accountId,
        int page,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(1, page);

        var pagedEntities = await _notificationRepository.GetPagedByReceiverAccountIdAsync(
            accountId,
            normalizedPage,
            DefaultPageSize,
            cancellationToken);

        var unreadCount = await _notificationRepository.GetUnreadCountByReceiverAccountIdAsync(
            accountId,
            cancellationToken);

        var viewModels = pagedEntities.Items.Select(n => new NotificationItemViewModel
        {
            NotificationId = n.NotificationId,
            Title = n.Title,
            Content = n.Content,
            Type = n.Type,
            RelatedObjectId = n.RelatedObjectId,
            IsRead = n.IsRead,
            CreatedAtUtc = n.CreatedAt,
            CreatedAtVietnam = _clock.ToVietnamTime(n.CreatedAt)
        }).ToList();

        var pagedResult = new PagedResult<NotificationItemViewModel>(
            viewModels,
            pagedEntities.Page,
            pagedEntities.PageSize,
            pagedEntities.TotalItems);

        return new NotificationIndexViewModel
        {
            Notifications = pagedResult,
            UnreadCount = unreadCount
        };
    }

    public async Task<int> GetMyUnreadCountAsync(
        int accountId,
        CancellationToken cancellationToken = default)
    {
        return await _notificationRepository.GetUnreadCountByReceiverAccountIdAsync(
            accountId,
            cancellationToken);
    }

    public async Task<ServiceResult> MarkAsReadAsync(
        int notificationId,
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _notificationRepository.GetByIdAndReceiverAccountIdAsync(
            notificationId,
            accountId,
            cancellationToken);

        if (notification is null)
        {
            return ServiceResult.Failure(
                ErrorCodes.NotificationNotFound,
                "Không tìm thấy thông báo hoặc bạn không có quyền truy cập.");
        }

        if (notification.IsRead)
        {
            return ServiceResult.Success();
        }

        notification.IsRead = true;
        notification.ReadAt = _clock.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> MarkAllAsReadAsync(
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var unreadList = await _notificationRepository.GetUnreadByReceiverAccountIdAsync(
            accountId,
            cancellationToken);

        if (unreadList.Count == 0)
        {
            return ServiceResult.Success();
        }

        var now = _clock.UtcNow;
        foreach (var notification in unreadList)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }
}
