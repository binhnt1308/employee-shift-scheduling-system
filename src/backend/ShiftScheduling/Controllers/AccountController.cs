using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Services.Accounts;
using ShiftScheduling.ViewModels.Account;

namespace ShiftScheduling.Controllers;

public class AccountController : Controller
{
    private readonly IAccountService _accountService;

    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var account = await _accountService.AuthenticateAsync(
            model.Username,
            model.Password,
            cancellationToken);

        if (account is null)
        {
            ModelState.AddModelError(
                string.Empty,
                "Tên đăng nhập hoặc mật khẩu không đúng, hoặc tài khoản đã bị khóa.");

            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.UserAccountId.ToString()),
            new(ClaimTypes.Name, account.Username),
            new(ClaimTypes.Role, account.Role.ToString())
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl)
            && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }
        // Hiển thị danh sách tài khoản và hỗ trợ tìm kiếm theo từ khóa.

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Index(
        string? keyword,
        CancellationToken cancellationToken)
    {
        var model = await _accountService.GetIndexAsync(
            keyword,
            cancellationToken);

        return View(model);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Create(
        CancellationToken cancellationToken)
    {
        var model = await _accountService.GetCreateAsync(cancellationToken);
        return View(model);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        AccountCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.EmployeeOptions = (await _accountService.GetCreateAsync(cancellationToken))
                .EmployeeOptions;
            return View(model);
        }

        var result = await _accountService.CreateAsync(
            model,
            GetCurrentAccountId(),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            model.EmployeeOptions = (await _accountService.GetCreateAsync(cancellationToken))
                .EmployeeOptions;
            return View(model);
        }

        TempData["Success"] = "Đã tạo tài khoản mới.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Edit(
        int id,
        CancellationToken cancellationToken)
    {
        var model = await _accountService.GetEditAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [Authorize(Roles = "Admin")]
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
            var current = await _accountService.GetEditAsync(id, cancellationToken);
            if (current is null)
            {
                return NotFound();
            }

            model.EmployeeOptions = current.EmployeeOptions;
            model.IsLocked = current.IsLocked;
            return View(model);
        }

        var result = await _accountService.UpdateAsync(
            id,
            model,
            GetCurrentAccountId(),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            var current = await _accountService.GetEditAsync(id, cancellationToken);
            if (current is not null)
            {
                model.EmployeeOptions = current.EmployeeOptions;
                model.IsLocked = current.IsLocked;
            }

            return View(model);
        }

        TempData["Success"] = "Đã cập nhật tài khoản.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetLock(
        int id,
        bool isLocked,
        CancellationToken cancellationToken)
    {
        var result = await _accountService.SetLockedAsync(
            id,
            isLocked,
            GetCurrentAccountId(),
            cancellationToken);

        TempData[result.Succeeded ? "Success" : "Error"] =
            result.Succeeded
                ? isLocked
                    ? "Đã khóa tài khoản."
                    : "Đã mở khóa tài khoản."
                : result.ErrorMessage;

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> ResetPassword(
        int id,
        CancellationToken cancellationToken)
    {
        var model = await _accountService.GetResetPasswordAsync(
            id,
            cancellationToken);

        return model is null ? NotFound() : View(model);
    }

    [Authorize(Roles = "Admin")]
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
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return View(model);
        }

        TempData["Success"] = $"Đã đặt lại mật khẩu cho tài khoản {model.Username}.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private int GetCurrentAccountId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(value, out var accountId))
        {
            throw new InvalidOperationException(
                "Không xác định được tài khoản đang đăng nhập.");
        }

        return accountId;
    }
}
