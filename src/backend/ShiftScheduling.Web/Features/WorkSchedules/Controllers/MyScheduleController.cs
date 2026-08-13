using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Models.Constants;

namespace ShiftScheduling.Features.WorkSchedules.Controllers;

/// <summary>
/// Controller cho phep Nhan vien xem lich lam viec chinh thuc da cong bo cua ban than (UC08).
/// </summary>
[Authorize(Roles = RoleNames.Employee)]
public sealed class MyScheduleController : AuthorizedController
{
    private readonly IWorkScheduleService _workScheduleService;

    public MyScheduleController(IWorkScheduleService workScheduleService)
    {
        _workScheduleService = workScheduleService;
    }

    /// <summary>
    /// Hien thi lich lam viec chinh thuc cua Nhan vien.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(int? id, CancellationToken cancellationToken)
    {
        var model = await _workScheduleService.GetEmployeeMyScheduleAsync(CurrentAccountId, id, cancellationToken);
        return View(model);
    }
}
