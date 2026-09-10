using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Features.Notifications.Services;

namespace ShiftScheduling.Features.Notifications.Controllers;

[Authorize]
public sealed class NotificationController : AuthorizedController
{
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var model = await _notificationService.GetMyNotificationsAsync(
            CurrentAccountId,
            page,
            cancellationToken);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(
        int notificationId,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await _notificationService.MarkAsReadAsync(
            notificationId,
            CurrentAccountId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index), new { page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        await _notificationService.MarkAllAsReadAsync(
            CurrentAccountId,
            cancellationToken);

        TempData["Success"] = "Đã đánh dấu tất cả thông báo là đã đọc.";
        return RedirectToAction(nameof(Index));
    }
}
