using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Services.ShiftTypes;
using ShiftScheduling.ViewModels.ShiftType;

namespace ShiftScheduling.Controllers;

[Authorize(Roles = "Admin,Manager")]
public class ShiftTypeController : Controller
{
    private readonly IShiftTypeService _shiftTypeService;

    public ShiftTypeController(IShiftTypeService shiftTypeService)
    {
        _shiftTypeService = shiftTypeService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var model = await _shiftTypeService.GetAllAsync(cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
        int id,
        CancellationToken cancellationToken)
    {
        var model = await _shiftTypeService.GetEditAsync(
            id,
            cancellationToken);

        return model is null
            ? NotFound()
            : View(model);
    }

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

        var result = await _shiftTypeService.UpdateAsync(
            id,
            model,
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage ?? "Không thể cập nhật ca làm.");

            return View(model);
        }

        TempData["Success"] = "Đã cập nhật ca làm việc.";
        return RedirectToAction(nameof(Index));
    }
}
