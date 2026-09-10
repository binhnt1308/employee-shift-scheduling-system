using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.Notifications.Components;
using ShiftScheduling.Features.Notifications.Controllers;
using ShiftScheduling.Features.Notifications.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Tests.Fakes;
using Xunit;

namespace ShiftScheduling.Tests.Notifications;

public sealed class NotificationSecurityTests
{
    private readonly FakeNotificationRepository _notificationRepo = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new();
    private readonly NotificationService _service;

    public NotificationSecurityTests()
    {
        _service = new NotificationService(_notificationRepo, _unitOfWork, _clock);
    }

    [Fact(DisplayName = "TC-NC10-07 - IDOR: Account A cannot mark Account B notification as read")]
    public async Task MarkAsReadAsync_WhenNotificationBelongsToDifferentAccount_FailsAndDoesNotModifyData()
    {
        const int victimAccountId = 10;
        const int attackerAccountId = 99;

        var victimNotification = new Notification
        {
            ReceiverAccountId = victimAccountId,
            Title = "Private Alert",
            Content = "Sensitive shift info",
            Type = NotificationType.ShiftTransferRequest,
            RelatedObjectId = 5,
            IsRead = false,
            CreatedAt = _clock.UtcNow
        };
        _notificationRepo.Add(victimNotification);

        var result = await _service.MarkAsReadAsync(victimNotification.NotificationId, attackerAccountId);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.NotificationNotFound, result.ErrorCode);
        Assert.False(victimNotification.IsRead);
        Assert.Null(victimNotification.ReadAt);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact(DisplayName = "TC-NC10-09 - NotificationController is decorated with AuthorizeAttribute")]
    public void NotificationController_HasAuthorizeAttribute()
    {
        var authAttr = typeof(NotificationController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
    }

    [Theory(DisplayName = "TC-NC10-10 - POST actions have HttpPost and ValidateAntiForgeryToken attributes")]
    [InlineData("MarkAsRead")]
    [InlineData("MarkAllAsRead")]
    public void NotificationController_PostActions_HaveRequiredSecurityAttributes(string actionName)
    {
        var method = typeof(NotificationController).GetMethod(actionName);
        Assert.NotNull(method);

        var httpPostAttr = method.GetCustomAttribute<HttpPostAttribute>();
        var antiforgeryAttr = method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>();

        Assert.NotNull(httpPostAttr);
        Assert.NotNull(antiforgeryAttr);
    }

    [Fact(DisplayName = "TC-NC10-11 - Authenticated valid claim returns correct unread count")]
    public async Task NotificationBellViewComponent_AuthenticatedUser_FetchesUnreadCount()
    {
        const int accountId = 15;
        _notificationRepo.Add(new Notification
        {
            ReceiverAccountId = accountId,
            Title = "Unread 1",
            Content = "C",
            Type = NotificationType.SchedulePublished,
            IsRead = false,
            CreatedAt = _clock.UtcNow
        });

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, accountId.ToString()),
            new Claim(ClaimTypes.Name, "employee1")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var component = new NotificationBellViewComponent(_service)
        {
            ViewComponentContext = new ViewComponentContext
            {
                ViewContext = new Microsoft.AspNetCore.Mvc.Rendering.ViewContext
                {
                    HttpContext = new DefaultHttpContext { User = user }
                }
            }
        };

        var result = await component.InvokeAsync();

        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        Assert.Equal(1, viewResult.ViewData?.Model);
        Assert.Equal(1, _notificationRepo.GetUnreadCountCallCount);
    }

    [Fact(DisplayName = "TC-NC10-12 - Unauthenticated user returns zero")]
    public async Task NotificationBellViewComponent_UnauthenticatedUser_ReturnsZero()
    {
        var component = new NotificationBellViewComponent(_service)
        {
            ViewComponentContext = new ViewComponentContext
            {
                ViewContext = new Microsoft.AspNetCore.Mvc.Rendering.ViewContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
                }
            }
        };

        var result = await component.InvokeAsync();

        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        Assert.Equal(0, viewResult.ViewData?.Model);
        Assert.Equal(0, _notificationRepo.GetUnreadCountCallCount);
    }

    [Fact(DisplayName = "TC-NC10-13 - Authenticated invalid NameIdentifier returns zero without querying an account")]
    public async Task NotificationBellViewComponent_AuthenticatedInvalidNameIdentifier_ReturnsZeroWithoutQuerying()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "invalid-account-id"),
            new Claim(ClaimTypes.Name, "employee1")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var component = new NotificationBellViewComponent(_service)
        {
            ViewComponentContext = new ViewComponentContext
            {
                ViewContext = new Microsoft.AspNetCore.Mvc.Rendering.ViewContext
                {
                    HttpContext = new DefaultHttpContext { User = user }
                }
            }
        };

        var result = await component.InvokeAsync();

        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        Assert.Equal(0, viewResult.ViewData?.Model);
        Assert.Equal(0, _notificationRepo.GetUnreadCountCallCount);
    }
}
