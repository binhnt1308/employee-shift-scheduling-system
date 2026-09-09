using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.Notifications.Services;
using ShiftScheduling.Features.Notifications.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Tests.Fakes;
using Xunit;

namespace ShiftScheduling.Tests.Notifications;

public sealed class NotificationServiceTests
{
    private readonly FakeNotificationRepository _notificationRepo = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(new DateTime(2026, 8, 15, 8, 0, 0, DateTimeKind.Utc));
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _service = new NotificationService(_notificationRepo, _unitOfWork, _clock);
    }

    [Fact(DisplayName = "TC-NC10-01 - GetMyNotificationsAsync returns only notifications belonging to current account")]
    public async Task GetMyNotificationsAsync_ReturnsOnlyCurrentUserNotifications()
    {
        const int account1 = 10;
        const int account2 = 20;

        _notificationRepo.Add(CreateNotification(account1, "Notice 1", false, _clock.UtcNow.AddHours(-2)));
        _notificationRepo.Add(CreateNotification(account2, "Notice 2", false, _clock.UtcNow.AddHours(-1)));
        _notificationRepo.Add(CreateNotification(account1, "Notice 3", true, _clock.UtcNow));

        var result = await _service.GetMyNotificationsAsync(account1, 1);

        Assert.Equal(2, result.Notifications.TotalItems);
        Assert.All(result.Notifications.Items, item =>
        {
            Assert.Contains(item.Title, new[] { "Notice 1", "Notice 3" });
        });
    }

    [Fact(DisplayName = "TC-NC10-02 - GetMyNotificationsAsync sorts descending by CreatedAt and paginates properly")]
    public async Task GetMyNotificationsAsync_SortsAndPaginatesProperly()
    {
        const int accountId = 10;
        var baseTime = _clock.UtcNow;

        for (var i = 1; i <= 25; i++)
        {
            _notificationRepo.Add(CreateNotification(accountId, $"Notice {i:D2}", false, baseTime.AddMinutes(i)));
        }

        var page1 = await _service.GetMyNotificationsAsync(accountId, 1);
        var page2 = await _service.GetMyNotificationsAsync(accountId, 2);

        Assert.Equal(25, page1.Notifications.TotalItems);
        Assert.Equal(20, page1.Notifications.Items.Count);
        Assert.Equal(5, page2.Notifications.Items.Count);
        Assert.Equal("Notice 25", page1.Notifications.Items[0].Title);
        Assert.Equal("Notice 05", page2.Notifications.Items[0].Title);
    }

    [Fact(DisplayName = "TC-NC10-03 - GetMyUnreadCountAsync counts only unread notifications for specified account")]
    public async Task GetMyUnreadCountAsync_CountsOnlyUnreadForAccount()
    {
        const int targetAccount = 10;
        const int otherAccount = 20;

        _notificationRepo.Add(CreateNotification(targetAccount, "Unread 1", false, _clock.UtcNow));
        _notificationRepo.Add(CreateNotification(targetAccount, "Unread 2", false, _clock.UtcNow));
        _notificationRepo.Add(CreateNotification(targetAccount, "Read 1", true, _clock.UtcNow));
        _notificationRepo.Add(CreateNotification(otherAccount, "Other Unread", false, _clock.UtcNow));

        var count = await _service.GetMyUnreadCountAsync(targetAccount);

        Assert.Equal(2, count);
    }

    [Fact(DisplayName = "TC-NC10-04 - MarkAsReadAsync marks own notification as read and sets ReadAt from clock")]
    public async Task MarkAsReadAsync_WhenOwnNotification_MarksReadAndSaves()
    {
        const int accountId = 10;
        var notification = CreateNotification(accountId, "Pending Notice", false, _clock.UtcNow.AddHours(-1));
        _notificationRepo.Add(notification);

        var result = await _service.MarkAsReadAsync(notification.NotificationId, accountId);

        Assert.True(result.Succeeded);
        Assert.True(notification.IsRead);
        Assert.Equal(_clock.UtcNow, notification.ReadAt);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact(DisplayName = "TC-NC10-05 - MarkAsReadAsync is idempotent when notification is already read")]
    public async Task MarkAsReadAsync_WhenAlreadyRead_ReturnsSuccessWithoutSavingAgain()
    {
        const int accountId = 10;
        var pastReadAt = _clock.UtcNow.AddDays(-1);
        var notification = CreateNotification(accountId, "Already Read Notice", true, _clock.UtcNow.AddDays(-2));
        notification.ReadAt = pastReadAt;
        _notificationRepo.Add(notification);

        var result = await _service.MarkAsReadAsync(notification.NotificationId, accountId);

        Assert.True(result.Succeeded);
        Assert.Equal(pastReadAt, notification.ReadAt);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact(DisplayName = "TC-NC10-06 - MarkAllAsReadAsync updates only unread notifications for current account")]
    public async Task MarkAllAsReadAsync_UpdatesOnlyUnreadForCurrentAccount()
    {
        const int account1 = 10;
        const int account2 = 20;

        var n1 = CreateNotification(account1, "A1 Unread 1", false, _clock.UtcNow);
        var n2 = CreateNotification(account1, "A1 Unread 2", false, _clock.UtcNow);
        var n3 = CreateNotification(account1, "A1 Read", true, _clock.UtcNow);
        var otherUnread = CreateNotification(account2, "A2 Unread", false, _clock.UtcNow);

        _notificationRepo.Add(n1);
        _notificationRepo.Add(n2);
        _notificationRepo.Add(n3);
        _notificationRepo.Add(otherUnread);

        var result = await _service.MarkAllAsReadAsync(account1);

        Assert.True(result.Succeeded);
        Assert.True(n1.IsRead);
        Assert.True(n2.IsRead);
        Assert.Equal(_clock.UtcNow, n1.ReadAt);
        Assert.Equal(_clock.UtcNow, n2.ReadAt);
        Assert.False(otherUnread.IsRead);
        Assert.Null(otherUnread.ReadAt);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Theory(DisplayName = "TC-NC10-08 - NotificationItemViewModel resolves correct target controller and action")]
    [InlineData(NotificationType.SchedulePublished, 100, "MySchedule", "Index")]
    [InlineData(NotificationType.ScheduleChanged, 100, "MySchedule", "Index")]
    [InlineData(NotificationType.LeaveRequestResult, 200, "LeaveRequest", "Details")]
    [InlineData(NotificationType.LeaveRequestSubmitted, 200, "LeaveRequest", "Details")]
    [InlineData(NotificationType.ShiftTransferRequest, 300, "ShiftTransfer", "Details")]
    [InlineData(NotificationType.ShiftTransferResult, 400, "ShiftTransfer", "Details")]
    public void NotificationItemViewModel_ResolvesCorrectTargetRoutes(
        NotificationType type,
        int relatedObjectId,
        string expectedController,
        string expectedAction)
    {
        var vm = new NotificationItemViewModel
        {
            NotificationId = 1,
            Title = "Test",
            Type = type,
            RelatedObjectId = relatedObjectId,
            CreatedAtUtc = _clock.UtcNow,
            CreatedAtVietnam = _clock.ToVietnamTime(_clock.UtcNow)
        };

        Assert.Equal(expectedController, vm.TargetController);
        Assert.Equal(expectedAction, vm.TargetAction);
        Assert.Equal(relatedObjectId, vm.TargetId);
    }

    [Fact(DisplayName = "TC-NC10-14 - MarkAllAsReadAsync with no unread notifications succeeds without saving")]
    public async Task MarkAllAsReadAsync_WhenNoUnreadNotifications_SucceedsWithoutSaving()
    {
        const int accountId = 10;
        var pastReadAt = _clock.UtcNow.AddDays(-1);
        var readNotification = CreateNotification(accountId, "Already Read", true, _clock.UtcNow.AddDays(-2));
        readNotification.ReadAt = pastReadAt;
        _notificationRepo.Add(readNotification);

        var result = await _service.MarkAllAsReadAsync(accountId);

        Assert.True(result.Succeeded);
        Assert.True(readNotification.IsRead);
        Assert.Equal(pastReadAt, readNotification.ReadAt);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact(DisplayName = "TC-NC10-15 - FakeNotificationRepository generates monotonically increasing ID when list has non-contiguous IDs")]
    public void FakeNotificationRepository_Add_WhenExistingIdsAreNonContiguous_AssignsMaxPlusOne()
    {
        var repo = new FakeNotificationRepository();
        repo.Items.Add(new Notification { NotificationId = 1, Title = "N1" });
        repo.Items.Add(new Notification { NotificationId = 3, Title = "N3" });

        var newNotification = new Notification { NotificationId = 0, Title = "New" };
        repo.Add(newNotification);

        Assert.Equal(4, newNotification.NotificationId);
    }

    private static Notification CreateNotification(
        int receiverAccountId,
        string title,
        bool isRead,
        DateTime createdAt) => new()
        {
            ReceiverAccountId = receiverAccountId,
            Title = title,
            Content = $"Content for {title}",
            Type = NotificationType.SchedulePublished,
            RelatedObjectId = 1,
            IsRead = isRead,
            CreatedAt = createdAt
        };
}
