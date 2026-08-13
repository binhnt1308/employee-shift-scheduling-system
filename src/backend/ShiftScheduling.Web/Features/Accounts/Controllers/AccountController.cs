using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Features.Accounts.Services;
using ShiftScheduling.Models.Constants;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Accounts.Controllers;

// BR01: Chi quan tri vien duoc tao, khoa, mo khoa va phan quyen tai khoan.
[Authorize(Roles = RoleNames.Admin)]
public sealed class AccountController : AuthorizedController
{
    private readonly IAccountService _accountService;

    /// <summary>
    /// Khoi tao AccountController voi AccountService.
    /// </summary>
    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    /// <summary>
    /// Lay danh sach tai khoan co tim kiem va phan trang.
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
    /// Hien thi form tao moi tai khoan nguoi dung.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return View(await _accountService.GetCreateAsync(cancellationToken));
    }

    /// <summary>
    /// Xu ly tao moi tai khoan nguoi dung vao he thong.
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
    /// Hien thi form cap nhat thong tin va vai tro tai khoan theo ID.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var model = await _accountService.GetEditAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    /// <summary>
    /// Xu ly cap nhat thong tin va vai tro tai khoan theo ID.
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
    /// Thay doi trang thai Khoa / Mo khoa tai khoan nguoi dung.
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
    /// Hien thi form dat lai mat khau cho tai khoan.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ResetPassword(int id, CancellationToken cancellationToken)
    {
        var model = await _accountService.GetResetPasswordAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    /// <summary>
    /// Xu ly dat lai mat khau moi cho tai khoan.
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
    /// Tai lai danh sach nhan vien cho dropdownlist khi tao tai khoan bi loi Validate.
    /// </summary>
    private async Task ReloadEmployeeOptionsAsync(
        AccountCreateViewModel model,
        CancellationToken cancellationToken)
    {
        model.EmployeeOptions = (await _accountService.GetCreateAsync(cancellationToken)).EmployeeOptions;
    }

    /// <summary>
    /// Tai lai danh sach nhan vien cho dropdownlist khi cap nhat tai khoan bi loi Validate.
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
