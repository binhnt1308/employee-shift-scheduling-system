using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Models;
using ShiftScheduling.Features.Dashboard.Services;

namespace ShiftScheduling.Features.Dashboard.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    private readonly IDashboardService _dashboardService;

    /// <summary>
    /// Khoi tao Controller tong quan voi DashboardService duoc tiem vao.
    /// </summary>
    public HomeController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Hien thi trang chu tong quan he thong voi cac con so thong ke dong.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var viewModel = await _dashboardService.GetDashboardAsync(cancellationToken);
        return View(viewModel);
    }

    /// <summary>
    /// Trang hien thi thong bao loi he thong.
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
