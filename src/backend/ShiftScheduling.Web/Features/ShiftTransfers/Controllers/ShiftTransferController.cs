using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Features.ShiftTransfers.Services;
using ShiftScheduling.Features.ShiftTransfers.ViewModels;
using ShiftScheduling.Models.Constants;

namespace ShiftScheduling.Features.ShiftTransfers.Controllers;

/// <summary>
/// Điều phối UC10 - chuyển một ca chính thức từ người gửi sang người nhận.
/// </summary>
[Authorize(Roles = RoleNames.ManagerOrEmployee)]
public sealed class ShiftTransferController : AuthorizedController
{
    private readonly IShiftTransferService _shiftTransferService;

    public ShiftTransferController(IShiftTransferService shiftTransferService)
    {
        _shiftTransferService = shiftTransferService;
    }

    /// <summary>
    /// Hiển thị yêu cầu đổi ca theo phạm vi của Nhân viên hoặc Quản lý.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        ShiftTransferStatusFilter? status,
        CancellationToken cancellationToken)
    {
        var model = await _shiftTransferService.GetIndexAsync(
            CurrentAccountId,
            User.IsInRole(RoleNames.Manager),
            status,
            cancellationToken);
        return View(model);
    }

    /// <summary>
    /// Chọn ca chính thức và chuẩn bị danh sách đồng nghiệp có khả năng nhận ca.
    /// </summary>
    [Authorize(Roles = RoleNames.Employee)]
    [HttpGet]
    public async Task<IActionResult> Create(
        int? shiftAssignmentId,
        CancellationToken cancellationToken)
    {
        var seed = new ShiftTransferCreateViewModel
        {
            ShiftAssignmentId = shiftAssignmentId ?? 0
        };
        var model = await _shiftTransferService.PrepareCreateAsync(
            CurrentAccountId,
            seed,
            cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    /// <summary>
    /// Gửi yêu cầu chuyển ca của chính Nhân viên đang đăng nhập.
    /// </summary>
    [Authorize(Roles = RoleNames.Employee)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        ShiftTransferCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await _shiftTransferService.PrepareCreateAsync(CurrentAccountId, model, cancellationToken);
            return View(model);
        }

        var result = await _shiftTransferService.SubmitAsync(model, CurrentAccountId, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            await _shiftTransferService.PrepareCreateAsync(CurrentAccountId, model, cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Đã gửi yêu cầu đổi ca cho người nhận.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Hiển thị chi tiết yêu cầu với kiểm tra phạm vi người gửi và người nhận.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var model = await _shiftTransferService.GetDetailsAsync(
            id,
            CurrentAccountId,
            User.IsInRole(RoleNames.Manager),
            cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    /// <summary>
    /// Ghi nhận quyết định đồng ý hoặc từ chối nhận ca.
    /// </summary>
    [Authorize(Roles = RoleNames.Employee)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Respond(
        int id,
        bool accept,
        CancellationToken cancellationToken)
    {
        var result = await _shiftTransferService.RespondAsync(id, accept, CurrentAccountId, cancellationToken);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? accept ? "Đã đồng ý nhận ca. Yêu cầu đang chờ Quản lý duyệt." : "Đã từ chối nhận ca."
            : result.ErrorMessage;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Hiển thị kết quả kiểm tra mới nhất trước khi Quản lý duyệt yêu cầu.
    /// </summary>
    [Authorize(Roles = RoleNames.Manager)]
    [HttpGet]
    public async Task<IActionResult> Review(int id, CancellationToken cancellationToken)
    {
        var model = await _shiftTransferService.PrepareReviewAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    /// <summary>
    /// Duyệt hoặc từ chối yêu cầu đổi ca và cập nhật lịch chính thức khi hợp lệ.
    /// </summary>
    [Authorize(Roles = RoleNames.Manager)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(
        int id,
        ShiftTransferReviewViewModel model,
        bool approve,
        CancellationToken cancellationToken)
    {
        if (id != model.ShiftTransferRequestId)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            var latest = await _shiftTransferService.PrepareReviewAsync(id, cancellationToken);
            if (latest is null)
            {
                return NotFound();
            }

            latest.ManagerNote = model.ManagerNote;
            return View(latest);
        }

        var result = await _shiftTransferService.ReviewAsync(
            id,
            approve,
            model.ManagerNote,
            CurrentAccountId,
            cancellationToken);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? approve ? "Đã duyệt và cập nhật lịch chính thức." : "Đã từ chối yêu cầu đổi ca."
            : result.ErrorMessage;
        return RedirectToAction(nameof(Index));
    }
}
