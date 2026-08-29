using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Features.SchedulePeriods.ViewModels;
using ShiftScheduling.Models.Constants;

namespace ShiftScheduling.Features.SchedulePeriods.Controllers;

/// <summary>
/// Controller quan ly ky lich va dinh muc nhan su (FR03/UC03).
/// </summary>
// BR01: Chi Quan ly cua hang duoc truy cap chuc nang thiet lap ky lich va dinh muc ca.
[Authorize(Roles = RoleNames.Manager)]
public sealed class SchedulePeriodController : AuthorizedController
{
    private readonly ISchedulePeriodService _schedulePeriodService;
    private readonly IClock _clock;

    public SchedulePeriodController(
        ISchedulePeriodService schedulePeriodService,
        IClock clock)
    {
        _schedulePeriodService = schedulePeriodService;
        _clock = clock;
    }

    /// <summary>
    /// Hien thi danh sach ky lich.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var viewModel = await _schedulePeriodService.GetIndexAsync(page, pageSize, cancellationToken);
        return View(viewModel);
    }

    /// <summary>
    /// Form tao ky lich moi.
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        var todayInVietnam = _clock.TodayInVietnam.ToDateTime(TimeOnly.MinValue);
        var model = new SchedulePeriodCreateViewModel
        {
            // Next Monday
            WeekStartDate = DateOnly.FromDateTime(
                todayInVietnam.AddDays(7 - (int)todayInVietnam.DayOfWeek + 1)),
            AvailabilityDeadline = todayInVietnam.AddDays(5).AddHours(23).AddMinutes(59)
        };
        return View(model);
    }

    /// <summary>
    /// Xu ly tao ky lich moi.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        SchedulePeriodCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _schedulePeriodService.CreateAsync(model, CurrentAccountId, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể tạo kỳ lịch.");
            return View(model);
        }

        TempData["StatusMessage"] = "Khởi tạo kỳ lịch 7 ngày và 21 định mức ca thành công.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Xem chi tiet ky lich va 21 dinh muc ca.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(
        int id,
        CancellationToken cancellationToken)
    {
        var viewModel = await _schedulePeriodService.GetDetailsAsync(id, cancellationToken);
        if (viewModel is null)
        {
            return NotFound();
        }

        return View(viewModel);
    }

    /// <summary>
    /// Form chinh sua dinh muc nhan su 21 ca trong tuan.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> EditStaffing(
        int id,
        CancellationToken cancellationToken)
    {
        var viewModel = await _schedulePeriodService.GetEditStaffingAsync(id, cancellationToken);
        if (viewModel is null)
        {
            return NotFound();
        }

        return View(viewModel);
    }

    /// <summary>
    /// Xu ly cap nhat dinh muc nhan su 21 ca trong tuan.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditStaffing(
        StaffingRequirementEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _schedulePeriodService.UpdateStaffingAsync(model, CurrentAccountId, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể cập nhật định mức.");
            return View(model);
        }

        TempData["StatusMessage"] = "Cập nhật định mức nhân sự thành công.";
        return RedirectToAction(nameof(Details), new { id = model.SchedulePeriodId });
    }

    /// <summary>
    /// Xu ly mo dang ky lich ranh cho ky lich.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenRegistration(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _schedulePeriodService.OpenRegistrationAsync(id, CurrentAccountId, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["StatusMessage"] = "Đã mở đăng ký lịch rảnh thành công cho kỳ lịch.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
