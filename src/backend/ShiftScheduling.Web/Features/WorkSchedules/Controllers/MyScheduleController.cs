using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Models.Constants;

namespace ShiftScheduling.Features.WorkSchedules.Controllers;

/// <summary>
/// Controller cho phép Nhân viên xem lịch làm việc đã công bố của bản thân (UC08).
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
    /// Hiển thị lịch làm việc chính thức của Nhân viên.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(int? id, CancellationToken cancellationToken)
    {
        var model = await _workScheduleService.GetEmployeeMyScheduleAsync(CurrentAccountId, id, cancellationToken);
        return View(model);
    }
}
