using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Features.Reports.Services;
using ShiftScheduling.Features.Reports.ViewModels;
using ShiftScheduling.Models.Constants;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Reports.Controllers;

/// <summary>
/// Controller tổng hợp báo cáo số ca, số ngày và tổng giờ làm (UC11).
/// </summary>
public sealed class ReportController : AuthorizedController
{
    private readonly IWorkReportService _reportService;

    public ReportController(IWorkReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// Màn hình Quản lý xem và lọc báo cáo tổng hợp (UC11).
    /// </summary>
    [Authorize(Roles = RoleNames.Manager)]
    [HttpGet]
    public async Task<IActionResult> Index(
        int? schedulePeriodId,
        int? employeeId,
        EmployeePosition? position,
        EmploymentType? employmentType,
        CancellationToken cancellationToken)
    {
        var model = await _reportService.GetManagerReportOverviewAsync(
            schedulePeriodId ?? 0, employeeId, position, employmentType, cancellationToken);

        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    /// <summary>
    /// Màn hình Nhân viên xem báo cáo cá nhân của bản thân (UC11).
    /// </summary>
    [Authorize(Roles = RoleNames.Employee)]
    [HttpGet]
    public async Task<IActionResult> MyReport(
        int? schedulePeriodId,
        CancellationToken cancellationToken)
    {
        var model = await _reportService.GetEmployeeOwnReportAsync(
            CurrentAccountId, schedulePeriodId ?? 0, cancellationToken);

        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    /// <summary>
    /// Xuat file báo cáo dạng CSV theo kỳ lịch va dieu kien loc (UC11).
    /// </summary>
    [Authorize(Roles = RoleNames.Manager)]
    [HttpGet]
    public async Task<IActionResult> Export(
        int schedulePeriodId,
        int? employeeId,
        EmployeePosition? position,
        EmploymentType? employmentType,
        CancellationToken cancellationToken)
    {
        var fileBytes = await _reportService.GenerateReportCsvAsync(
            schedulePeriodId, employeeId, position, employmentType, cancellationToken);

        var fileName = $"BaoCaoTongHopCa_KyLich_{schedulePeriodId}_{DateTime.Now:yyyyMMddHHmmss}.csv";
        return File(fileBytes, "text/csv; charset=utf-8", fileName);
    }

    /// <summary>
    /// Điều chỉnh hoặc đặt lại giờ công thực tế của phân công ca làm việc (NC-07).
    /// </summary>
    [Authorize(Roles = RoleNames.Manager)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustWorkdayHours(
        AdjustWorkdayHoursRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _reportService.AdjustWorkdayHoursAsync(
            CurrentAccountId, request, cancellationToken);

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = request.IsReset
                ? "Đặt lại theo hệ thống thành công."
                : "Điều chỉnh giờ công thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }

        return RedirectToAction(nameof(Index), new
        {
            schedulePeriodId = request.SchedulePeriodId,
            employeeId = request.ReturnEmployeeId,
            position = request.ReturnPosition,
            employmentType = request.ReturnEmploymentType
        });
    }
}
