using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Services.Employees;
using ShiftScheduling.ViewModels.Employee;

namespace ShiftScheduling.Controllers;

[Authorize(Roles = "Admin,Manager")]
public class EmployeeController : Controller
{
    private readonly IEmployeeService _employeeService;

    public EmployeeController(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? keyword,
        CancellationToken cancellationToken)
    {
        var model = await _employeeService.GetIndexAsync(
            keyword,
            cancellationToken);

        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new EmployeeFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        EmployeeFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _employeeService.CreateAsync(
            model,
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage ?? "Không thể thêm nhân viên.");

            return View(model);
        }

        TempData["Success"] = "Đã thêm nhân viên thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
        int id,
        CancellationToken cancellationToken)
    {
        var model = await _employeeService.GetFormAsync(
            id,
            cancellationToken);

        return model is null
            ? NotFound()
            : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        EmployeeFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.EmployeeId)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _employeeService.UpdateAsync(
            id,
            model,
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage ?? "Không thể cập nhật nhân viên.");

            return View(model);
        }

        TempData["Success"] = "Đã cập nhật hồ sơ nhân viên.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(
        int id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var result = await _employeeService.SetActiveAsync(
            id,
            isActive,
            cancellationToken);

        TempData[result.Succeeded ? "Success" : "Error"] =
            result.Succeeded
                ? (isActive
                    ? "Đã mở lại trạng thái hoạt động của nhân viên."
                    : "Đã chuyển nhân viên sang trạng thái ngừng hoạt động.")
                : result.ErrorMessage;

        return RedirectToAction(nameof(Index));
    }
}
