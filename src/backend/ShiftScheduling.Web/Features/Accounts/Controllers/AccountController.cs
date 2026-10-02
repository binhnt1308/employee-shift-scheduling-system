using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Features.Accounts.Services;
using ShiftScheduling.Models.Constants;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Accounts.Controllers;

// BR01: Chỉ quản trị viên được tạo, khóa, mở khóa và phân quyền tài khoản.
[Authorize(Roles = RoleNames.Admin)]
public sealed class AccountController : AuthorizedController
{
    private readonly IAccountService _accountService;

    /// <summary>
    /// Khởi tạo AccountController với AccountService.
    /// </summary>
    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    /// <summary>
    /// Lấy danh sách tài khoản có tìm kiếm và phân trang.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        string? keyword,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var model = await _accountService.GetIndexAsync(keyword, page, pageSize, cancellationToken);
        return View(model);
    }

    /// <summary>
    /// Hiển thị form tạo mới tài khoản người dùng.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return View(await _accountService.GetCreateAsync(cancellationToken));
    }

    /// <summary>
    /// Xử lý tạo mới tài khoản người dùng vào hệ thống.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        AccountCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await ReloadEmployeeOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var result = await _accountService.CreateAsync(model, CurrentAccountId, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            await ReloadEmployeeOptionsAsync(model, cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Đã tạo tài khoản mới.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Hiển thị form cập nhật thông tin và vai trò tài khoản theo ID.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var model = await _accountService.GetEditAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    /// <summary>
    /// Xử lý cập nhật thông tin và vai trò tài khoản theo ID.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        AccountEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.UserAccountId)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            await ReloadEditOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var result = await _accountService.UpdateAsync(id, model, CurrentAccountId, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            await ReloadEditOptionsAsync(model, cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Đã cập nhật tài khoản.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Thay đổi trạng thái Khóa / Mở khóa tài khoản người dùng.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetLock(
        int id,
        bool isLocked,
        CancellationToken cancellationToken)
    {
        var result = await _accountService.SetLockedAsync(id, isLocked, CurrentAccountId, cancellationToken);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? isLocked ? "Đã khóa tài khoản." : "Đã mở khóa tài khoản."
            : result.ErrorMessage;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Hiển thị form đặt lại mật khẩu cho tài khoản.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ResetPassword(int id, CancellationToken cancellationToken)
    {
        var model = await _accountService.GetResetPasswordAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    /// <summary>
    /// Xử lý đặt lại mật khẩu mới cho tài khoản.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(
        int id,
        AccountResetPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.UserAccountId)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _accountService.ResetPasswordAsync(
            id,
            model,
            CurrentAccountId,
            cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return View(model);
        }

        TempData["Success"] = $"Đã đặt lại mật khẩu cho tài khoản {model.Username}.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Tải lại danh sách nhân viên cho dropdown khi form tạo tài khoản không hợp lệ.
    /// </summary>
    private async Task ReloadEmployeeOptionsAsync(
        AccountCreateViewModel model,
        CancellationToken cancellationToken)
    {
        model.EmployeeOptions = (await _accountService.GetCreateAsync(cancellationToken)).EmployeeOptions;
    }

    /// <summary>
    /// Tải lại danh sách nhân viên cho dropdown khi form cập nhật tài khoản không hợp lệ.
    /// </summary>
    private async Task ReloadEditOptionsAsync(
        AccountEditViewModel model,
        CancellationToken cancellationToken)
    {
        var current = await _accountService.GetEditAsync(model.UserAccountId, cancellationToken);
        if (current is not null)
        {
            model.EmployeeOptions = current.EmployeeOptions;
            model.IsLocked = current.IsLocked;
        }
    }
}
