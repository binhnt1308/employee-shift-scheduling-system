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

    /// <summary>
    /// Khoi tao ShiftTypeController voi ShiftTypeService.
    /// </summary>
    public ShiftTypeController(IShiftTypeService shiftTypeService)
    {
        _shiftTypeService = shiftTypeService;
    }

    /// <summary>
    /// Lay danh sach tat ca cac ca lam viec co dinh trong he thong.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await _shiftTypeService.GetAllAsync(cancellationToken));
    }

    /// <summary>
    /// Hien thi form cap nhat thoi gian va cau hinh ca lam viec theo ID.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var model = await _shiftTypeService.GetEditAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    /// <summary>
    /// Xu ly cap nhat thoi gian va cau hinh ca lam viec theo ID.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        ShiftTypeEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.ShiftTypeId)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _shiftTypeService.UpdateAsync(id, model, CurrentAccountId, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return View(model);
        }

        TempData["Success"] = "Đã cập nhật ca làm việc.";
        return RedirectToAction(nameof(Index));
    }
}
