using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Features.Dashboard.Services;
using ShiftScheduling.Models;

namespace ShiftScheduling.Features.Dashboard.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    private readonly IDashboardService _dashboardService;

    /// <summary>
    /// Khởi tạo Controller tổng quan với DashboardService được tiêm vào.
    /// </summary>
    public HomeController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Hiển thị trang chủ tổng quan hệ thống với các số liệu thống kê động.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        int? userAccountId = null;
        var nameId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(nameId, out var id))
        {
            userAccountId = id;
        }

        var viewModel = await _dashboardService.GetDashboardAsync(userAccountId, cancellationToken);
        return View(viewModel);
    }

    /// <summary>
    /// Trang hiển thị thông báo lỗi hệ thống.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
