using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Features.SchedulePeriods.ViewModels;
using ShiftScheduling.Models.Constants;

namespace ShiftScheduling.Features.SchedulePeriods.Controllers;

/// <summary>
/// Controller quản lý kỳ lịch và định mức nhân sự (FR03/UC03).
/// </summary>
// BR01: Chỉ Quản lý cửa hàng được truy cập chức năng thiết lập kỳ lịch và định mức ca.
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
    /// Hiển thị danh sách kỳ lịch.
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
    /// Hiển thị form tạo kỳ lịch mới.
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        var todayInVietnam = _clock.TodayInVietnam;
        var daysUntilNextMonday = todayInVietnam.DayOfWeek switch
        {
            DayOfWeek.Sunday => 1,
            _ => 8 - (int)todayInVietnam.DayOfWeek
        };
        var nextMonday = todayInVietnam.AddDays(daysUntilNextMonday);
        var deadlineDate = nextMonday.AddDays(-1);
        var deadline = deadlineDate.ToDateTime(new TimeOnly(23, 59));

        var model = new SchedulePeriodCreateViewModel
        {
            WeekStartDate = nextMonday,
            AvailabilityDeadline = deadline
        };
        return View(model);
    }

    /// <summary>
    /// Xử lý tạo kỳ lịch mới.
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
    /// Xem chi tiết kỳ lịch và 21 định mức ca.
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
    /// Hiển thị form chỉnh sửa 21 định mức nhân sự trong tuần.
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
    /// Xử lý cập nhật 21 định mức nhân sự trong tuần.
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
    /// Xử lý mở đăng ký lịch rảnh cho kỳ lịch.
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
