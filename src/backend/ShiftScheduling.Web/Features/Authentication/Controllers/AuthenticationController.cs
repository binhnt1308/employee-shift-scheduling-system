using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShiftScheduling.Features.Authentication.Services;
using ShiftScheduling.Models.ViewModels;
using UserAuthenticationService =
    ShiftScheduling.Features.Authentication.Services.IAuthenticationService;

namespace ShiftScheduling.Features.Authentication.Controllers;

[Route("Account")]
public sealed class AuthenticationController : Controller
{
    private const string LoginViewPath = "~/Views/Account/Login.cshtml";
    private const string AccessDeniedViewPath = "~/Views/Account/AccessDenied.cshtml";

    private readonly UserAuthenticationService _authenticationService;

    public AuthenticationController(
        UserAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    [AllowAnonymous]
    [HttpGet("Login")]
    public IActionResult Login(string? returnUrl = null)
    {
        // Khong hien lai trang dang nhap khi nguoi dung da co phien hop le.
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(
            LoginViewPath,
            new LoginViewModel
            {
                ReturnUrl = returnUrl
            });
    }

    [AllowAnonymous]
    [HttpPost("Login")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(LoginViewPath, model);
        }

        var account = await _authenticationService.AuthenticateAsync(
            model.Username,
            model.Password,
            cancellationToken);

        // BR01: Tai khoan bi khoa hoac thong tin dang nhap sai khong duoc tao phien.
        if (account is null)
        {
            ModelState.AddModelError(
                string.Empty,
                "Tên đăng nhập hoặc mật khẩu không đúng, hoặc tài khoản đã bị khóa.");

            return View(LoginViewPath, model);
        }

        await SignInAsync(account);

        // Chi chuyen huong ve dia chi noi bo de tranh loi open redirect.
        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) &&
            Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpPost("Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet("AccessDenied")]
    public IActionResult AccessDenied()
    {
        return View(AccessDeniedViewPath);
    }

    private async Task SignInAsync(AuthenticatedAccount account)
    {
        // Chi dua thong tin dinh danh va phan quyen can thiet vao cookie.
        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                account.UserAccountId.ToString()),
            new Claim(
                ClaimTypes.Name,
                account.Username),
            new Claim(
                ClaimTypes.Role,
                account.Role.ToString())
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal);
    }
}
