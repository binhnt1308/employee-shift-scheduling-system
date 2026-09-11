using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Features.Availabilities.Services;
using ShiftScheduling.Features.Availabilities.ViewModels;
using ShiftScheduling.Models.Constants;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Availabilities.Controllers;

/// <summary>
/// Controller quản lý và đăng ký lịch rảnh (UC04/NC-02).
/// AR-03: Không gọi trực tiếp database – mọi logic đi qua IAvailabilityService.
/// </summary>
public sealed class AvailabilityController : AuthorizedController
{
    private readonly IAvailabilityService _availabilityService;

    public AvailabilityController(IAvailabilityService availabilityService)
    {
        _availabilityService = availabilityService;
    }

    /// <summary>Danh sách kỳ lịch và trạng thái đăng ký lịch rảnh của Nhân viên.</summary>
    [Authorize(Roles = RoleNames.Employee)]
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await _availabilityService.GetEmployeePeriodsAsync(CurrentAccountId, cancellationToken);
        return View(model);
    }

    /// <summary>Form đăng ký hoặc cập nhật lịch rảnh của Nhân viên.</summary>
    [Authorize(Roles = RoleNames.Employee)]
    [HttpGet]
    public async Task<IActionResult> Register(int id, CancellationToken cancellationToken)
    {
        var model = await _availabilityService.GetEmployeeRegisterFormAsync(
            CurrentAccountId, id, cancellationToken);

        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    /// <summary>
    /// Lưu lịch rảnh của Nhân viên cho kỳ lịch.
    /// NC-02: Nhận thêm shiftPreferences; server từ chối key/value không hợp lệ.
    /// </summary>
    [Authorize(Roles = RoleNames.Employee)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        int id,
        [FromForm] List<string> selectedShiftKeys,
        [FromForm] Dictionary<string, AvailabilityPreferenceLevel> shiftPreferences,
        CancellationToken cancellationToken)
    {
        // SE-04: Enum ngoài phạm vi ModelState sẽ invalid – trả lỗi sớm.
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Dữ liệu gửi lên không hợp lệ.";
            return RedirectToAction(nameof(Register), new { id });
        }

        var result = await _availabilityService.SaveEmployeeAvailabilityAsync(
            CurrentAccountId, id, selectedShiftKeys, shiftPreferences, cancellationToken);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Register), new { id });
        }

        TempData["Success"] = "Đã lưu lịch rảnh thành công.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Lưu mẫu lịch rảnh tuần (NC-02).
    /// AntiForgery + Employee auth. Server validate toàn bộ DayOfWeek/ShiftCode/PreferenceLevel.
    /// </summary>
    [Authorize(Roles = RoleNames.Employee)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WeeklyPattern(
        [FromForm] List<WeeklyPatternEntryInputViewModel> entries,
        [FromQuery] int? returnPeriodId,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Dữ liệu mẫu tuần không hợp lệ.";
            return returnPeriodId.HasValue
                ? RedirectToAction(nameof(Register), new { id = returnPeriodId.Value })
                : RedirectToAction(nameof(Index));
        }

        var result = await _availabilityService.SaveWeeklyPatternAsync(
            CurrentAccountId, entries, cancellationToken);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.ErrorMessage;
        }
        else
        {
            TempData["Success"] = "Đã lưu mẫu lịch rảnh tuần thành công.";
        }

        return returnPeriodId.HasValue
            ? RedirectToAction(nameof(Register), new { id = returnPeriodId.Value })
            : RedirectToAction(nameof(Index));
    }

    /// <summary>Màn hình Quản lý xem và lọc lịch rảnh của nhân viên (Read-only).</summary>
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

        return View(model);
    }
}
