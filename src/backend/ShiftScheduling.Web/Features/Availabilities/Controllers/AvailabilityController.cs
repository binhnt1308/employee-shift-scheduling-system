using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Features.Availabilities.Services;
using ShiftScheduling.Features.Availabilities.ViewModels;
using ShiftScheduling.Models.Constants;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Availabilities.Controllers;

/// <summary>
/// Controller quan ly va dang ky lich ranh (UC04).
/// </summary>
public sealed class AvailabilityController : AuthorizedController
{
    private readonly IAvailabilityService _availabilityService;

    public AvailabilityController(IAvailabilityService availabilityService)
    {
        _availabilityService = availabilityService;
    }

    /// <summary>
    /// Danh sach ky lich va trang thai dang ky lich ranh cua Nhan vien.
    /// </summary>
    [Authorize(Roles = RoleNames.Employee)]
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await _availabilityService.GetEmployeePeriodsAsync(CurrentAccountId, cancellationToken);
        return View(model);
    }

    /// <summary>
    /// Form dang ky hoac cap nhat lich ranh cua Nhan vien.
    /// </summary>
    [Authorize(Roles = RoleNames.Employee)]
    [HttpGet]
    public async Task<IActionResult> Register(int id, CancellationToken cancellationToken)
    {
        var model = await _availabilityService.GetEmployeeRegisterFormAsync(CurrentAccountId, id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    /// <summary>
    /// Xu ly dang ky hoac cap nhat lich ranh cua Nhan vien.
    /// </summary>
    [Authorize(Roles = RoleNames.Employee)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        int id,
        List<string> selectedShiftKeys,
        CancellationToken cancellationToken)
    {
        var result = await _availabilityService.SaveEmployeeAvailabilityAsync(
            CurrentAccountId, id, selectedShiftKeys, cancellationToken);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Register), new { id });
        }

        TempData["Success"] = "Đã lưu lịch rảnh thành công.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Man hinh Quan ly xem va loc lich ranh cua nhan vien (Read-only).
    /// </summary>
    [Authorize(Roles = RoleNames.Manager)]
    [HttpGet]
    public async Task<IActionResult> Overview(
        int? schedulePeriodId,
        int? employeeId,
        EmployeePosition? position,
        DateOnly? date,
        int? shiftTypeId,
        CancellationToken cancellationToken)
    {
        var model = await _availabilityService.GetManagerOverviewAsync(
            schedulePeriodId ?? 0, employeeId, position, date, shiftTypeId, cancellationToken);

        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }
}
