using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Features.LeaveRequests.Services;
using ShiftScheduling.Features.LeaveRequests.ViewModels;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Models.Constants;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.LeaveRequests.Controllers;

/// <summary>
/// Điều phối UC09 - gửi, theo dõi và xử lý đơn xin nghỉ.
/// </summary>
[Authorize(Roles = RoleNames.ManagerOrEmployee)]
public sealed class LeaveRequestController : AuthorizedController
{
    private readonly ILeaveRequestService _leaveRequestService;
    private readonly IWorkScheduleService? _workScheduleService;

    public LeaveRequestController(
        ILeaveRequestService leaveRequestService,
        IWorkScheduleService? workScheduleService = null)
    {
        _leaveRequestService = leaveRequestService;
        _workScheduleService = workScheduleService;
    }

    /// <summary>
    /// Hiển thị đơn của Nhân viên hoặc danh sách xử lý của Quản lý cửa hàng.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        int? schedulePeriodId,
        LeaveRequestStatus? status,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var model = await _leaveRequestService.GetIndexAsync(
            CurrentAccountId,
            User.IsInRole(RoleNames.Manager),
            schedulePeriodId,
            status,
            page,
            pageSize,
            cancellationToken);
        return View(model);
    }

    /// <summary>
    /// Hiển thị biểu mẫu gửi đơn xin nghỉ cho Nhân viên.
    /// </summary>
    [Authorize(Roles = RoleNames.Employee)]
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = await _leaveRequestService.PrepareCreateAsync(
            CurrentAccountId,
            cancellationToken: cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    /// <summary>
    /// Gửi đơn xin nghỉ của Nhân viên đang đăng nhập.
    /// </summary>
    [Authorize(Roles = RoleNames.Employee)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        LeaveRequestCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await _leaveRequestService.PrepareCreateAsync(CurrentAccountId, model, cancellationToken);
            return View(model);
        }

        var result = await _leaveRequestService.SubmitAsync(model, CurrentAccountId, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            await _leaveRequestService.PrepareCreateAsync(CurrentAccountId, model, cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Đã gửi đơn xin nghỉ. Đơn đang chờ Quản lý xử lý.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Hủy đơn xin nghỉ khi đơn còn ở trạng thái Chờ duyệt.
    /// </summary>
    [Authorize(Roles = RoleNames.Employee)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var result = await _leaveRequestService.CancelAsync(id, CurrentAccountId, cancellationToken);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? "Đã hủy đơn xin nghỉ."
            : result.ErrorMessage;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Hiển thị chi tiết đơn, có kiểm tra phạm vi dữ liệu của Nhân viên.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var model = await _leaveRequestService.GetDetailsAsync(
            id,
            CurrentAccountId,
            User.IsInRole(RoleNames.Manager),
            cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    /// <summary>
    /// Hiển thị dữ liệu mới nhất của đơn đang chờ Quản lý duyệt.
    /// </summary>
    [Authorize(Roles = RoleNames.Manager)]
    [HttpGet]
    public async Task<IActionResult> Review(int id, CancellationToken cancellationToken)
    {
        var model = await _leaveRequestService.PrepareReviewAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        if (_workScheduleService is not null
            && model.Details.HasPublishedAssignment
            && model.Details.Status == LeaveRequestStatus.Pending)
        {
            model.ReplacementPreview = await _workScheduleService.GetLeaveReplacementPreviewAsync(
                id, cancellationToken);
        }

        return View(model);
    }

    /// <summary>
    /// Duyệt hoặc từ chối đơn xin nghỉ theo quyết định của Quản lý.
    /// </summary>
    [Authorize(Roles = RoleNames.Manager)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(
        int id,
        LeaveRequestReviewViewModel model,
        bool approve,
        CancellationToken cancellationToken)
    {
        if (id != model.LeaveRequestId)
        {
            return BadRequest();
        }

        if (approve)
        {
            var details = await _leaveRequestService.GetDetailsAsync(id, CurrentAccountId, true, cancellationToken);
            if (details?.HasPublishedAssignment == true)
            {
                TempData["Error"] =
                    "Ca làm việc thuộc lịch đã công bố cần có người thay thế trước khi duyệt. " +
                    "Vui lòng chọn 'Bố trí người thay'.";
                return RedirectToAction(nameof(Review), new { id });
            }
        }

        if (!ModelState.IsValid)
        {
            var latest = await _leaveRequestService.PrepareReviewAsync(id, cancellationToken);
            if (latest is null)
            {
                return NotFound();
            }

            latest.ManagerNote = model.ManagerNote;
            return View(latest);
        }

        var result = await _leaveRequestService.ReviewAsync(
            id,
            approve,
            model.ManagerNote,
            CurrentAccountId,
            model.RowVersion,
            cancellationToken);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? approve ? "Đã duyệt đơn xin nghỉ." : "Đã từ chối đơn xin nghỉ."
            : result.ErrorMessage;
        return RedirectToAction(nameof(Index));
    }
}
