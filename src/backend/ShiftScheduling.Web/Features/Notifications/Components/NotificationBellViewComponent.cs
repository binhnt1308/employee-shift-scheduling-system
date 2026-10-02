using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Features.Notifications.Services;

namespace ShiftScheduling.Features.Notifications.Components;

// ViewComponent chuẩn bị dữ liệu thông báo dùng chung trước khi render partial tương ứng.
public sealed class NotificationBellViewComponent : ViewComponent
{
    private readonly INotificationService _notificationService;

    public NotificationBellViewComponent(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (UserClaimsPrincipal.Identity?.IsAuthenticated != true)
        {
            return View(0);
        }

        var accountIdClaim = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(accountIdClaim, out var accountId))
        {
            return View(0);
        }

        var unreadCount = await _notificationService.GetMyUnreadCountAsync(accountId, HttpContext.RequestAborted);
        return View(unreadCount);
    }
}
