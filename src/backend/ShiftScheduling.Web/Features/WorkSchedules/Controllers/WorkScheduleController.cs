using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Constants;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.WorkSchedules.Controllers;

/// <summary>
/// Controller quan ly phan cong ca (UC05), OR-Tools (UC06) va cong bo lich (UC07).
/// </summary>
[Authorize(Roles = RoleNames.Manager)]
public sealed class WorkScheduleController : AuthorizedController
{
    private readonly IWorkScheduleService _workScheduleService;

    public WorkScheduleController(IWorkScheduleService workScheduleService)
    {
        _workScheduleService = workScheduleService;
    }

    /// <summary>
    /// Danh sach cac ky lich va trang thai phan cong.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var model = await _workScheduleService.GetIndexAsync(page, pageSize, cancellationToken);
        return View(model);
    }

    /// <summary>
    /// Man hinh phan cong ca thu cong (Grid 7x3) va sinh lich OR-Tools.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var model = await _workScheduleService.GetEditGridAsync(id, CurrentAccountId, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    /// <summary>
    /// Lay danh sach nhan vien du dieu kien cho ca lam viec de phan cong.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetEligibleEmployees(
        int schedulePeriodId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken)
    {
        var options = await _workScheduleService.GetEligibleEmployeesForShiftAsync(
            schedulePeriodId, workDate, shiftTypeId, cancellationToken);
        return Json(options);
    }

    /// <summary>
    /// Xu ly phan cong hoac thay the nhan vien vao ca thu cong (AJAX / Form).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignManual(
        ManualAssignRequestViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await _workScheduleService.AssignEmployeeManualAsync(model, CurrentAccountId, cancellationToken);
        if (!result.Succeeded)
        {
            return Json(new { success = false, message = result.ErrorMessage });
        }

        return Json(new { success = true });
    }

    /// <summary>
    /// Xu ly xoa nhan vien khoi ca phan cong thu cong.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveManual(
        ManualRemoveRequestViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await _workScheduleService.RemoveEmployeeManualAsync(model, CurrentAccountId, cancellationToken);
        if (!result.Succeeded)
        {
            return Json(new { success = false, message = result.ErrorMessage });
        }

        return Json(new { success = true });
    }

    /// <summary>
    /// Luu lich nhap (BR14: Cho phep luu lich nhap chua hoan chinh).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDraft(int id, CancellationToken cancellationToken)
    {
        var result = await _workScheduleService.SaveDraftAsync(id, CurrentAccountId, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.ErrorMessage;
        }
        else
        {
            TempData["Success"] = "Đã lưu lịch làm việc ở trạng thái Nháp.";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    /// <summary>
    /// Sinh lich de xuat tu dong bang Google OR-Tools CP-SAT (UC06/BR15).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateOrToolsProposal(int id, CancellationToken cancellationToken)
    {
        var result = await _workScheduleService.GenerateOrToolsProposalAsync(id, CurrentAccountId, cancellationToken);
        return Json(result);
    }

    /// <summary>
    /// Ghi de phuong an de xuat OR-Tools vao lich nhap (UC06).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdoptProposal(
        int id,
        [FromBody] List<ProposedAssignmentItemViewModel> proposedAssignments,
        CancellationToken cancellationToken)
    {
        var result = await _workScheduleService.AdoptProposalToDraftAsync(
            id, proposedAssignments, CurrentAccountId, cancellationToken);
        if (!result.Succeeded)
        {
            return Json(new { success = false, message = result.ErrorMessage });
        }

        return Json(new { success = true });
    }

    /// <summary>
    /// Duyet va cong bo lich chinh thuc (UC07/BR14/BR16).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id, CancellationToken cancellationToken)
    {
        var result = await _workScheduleService.PublishScheduleAsync(id, CurrentAccountId, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Edit), new { id });
        }

        TempData["Success"] = "Đã duyệt và công bố chính thức lịch làm việc cho kỳ lịch.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    /// <summary>
    /// Hiển thị màn hình gợi ý và xếp người thay thế cho ca thiếu nhân sự khi đơn nghỉ được duyệt.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> LeaveReplacement(int leaveRequestId, CancellationToken cancellationToken)
    {
        var model = await _workScheduleService.GetLeaveReplacementAsync(leaveRequestId, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    /// <summary>
    /// Quản lý xác nhận xếp người thay thế cho ca thiếu nhân sự trên lịch đã công bố.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplaceApprovedLeaveAssignment(
        LeaveReplacementRequestViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Dữ liệu yêu cầu thay thế không hợp lệ.";
            return RedirectToAction(nameof(LeaveReplacement), new { leaveRequestId = model.LeaveRequestId });
        }

        var result = await _workScheduleService.ReplaceApprovedLeaveAssignmentAsync(
            model, CurrentAccountId, cancellationToken);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.ErrorMessage;
        }
        else
        {
            TempData["Success"] = "Đã xếp nhân viên thay thế thành công cho ca làm việc.";
        }

        return RedirectToAction(nameof(LeaveReplacement), new { leaveRequestId = model.LeaveRequestId });
    }
}
