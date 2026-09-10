using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Features.ShiftTypes.Services;
using ShiftScheduling.Models.Constants;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.ShiftTypes.Controllers;

// BR01: Chỉ Quản lý cửa hàng được cấu hình ca làm việc.
[Authorize(Roles = RoleNames.Manager)]
public sealed class ShiftTypeController : AuthorizedController
{
    private readonly IShiftTypeService _shiftTypeService;

    public ShiftTypeController(IShiftTypeService shiftTypeService)
    {
        _shiftTypeService = shiftTypeService;
    }

    /// <summary>
    /// Lấy danh sách tất cả các ca làm việc đang hoạt động trong hệ thống.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await _shiftTypeService.GetAllAsync(cancellationToken));
    }

    /// <summary>
    /// Hiển thị form cấu hình đồng thời ba ca làm việc theo phiên bản mới.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
    {
        var model = await _shiftTypeService.GetConfigurationAsync(cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    /// <summary>
    /// Xử lý cập nhật cấu hình ba ca làm việc đồng thời theo phiên bản mới.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        ShiftConfigurationEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _shiftTypeService.UpdateConfigurationAsync(
            model,
            CurrentAccountId,
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return View(model);
        }

        TempData["Success"] = result.SuccessMessage ?? "Đã cập nhật cấu hình giờ 3 ca thành công.";
        return RedirectToAction(nameof(Index));
    }
}
