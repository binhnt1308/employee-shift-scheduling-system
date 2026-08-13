using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Features.Employees.Services;
using ShiftScheduling.Models.Constants;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Employees.Controllers;

// BR01: Chi quan tri vien hoac quan ly duoc quan ly ho so nhan vien.
[Authorize(Roles = RoleNames.AdminOrManager)]
public sealed class EmployeeController : AuthorizedController
{
    private readonly IEmployeeService _employeeService;

    /// <summary>
    /// Khoi tao EmployeeController voi EmployeeService.
    /// </summary>
    public EmployeeController(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    /// <summary>
    /// Lay danh sach nhan vien co tim kiem theo tu khoa va phan trang.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        string? keyword,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var model = await _employeeService.GetIndexAsync(keyword, page, pageSize, cancellationToken);
        return View(model);
    }

    /// <summary>
    /// Hien thi form tao moi ho so nhan vien.
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        return View(new EmployeeFormViewModel());
    }

    /// <summary>
    /// Xu ly luu ho so nhan vien moi vao co so du lieu.
    /// </summary>
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

        var result = await _employeeService.CreateAsync(model, CurrentAccountId, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return View(model);
        }

        TempData["Success"] = "Đã thêm nhân viên thành công.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Hien thi form cap nhat thong tin nhan vien theo ID.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var model = await _employeeService.GetFormAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    /// <summary>
    /// Xu ly cap nhat thong tin nhan vien theo ID.
    /// </summary>
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

        var result = await _employeeService.UpdateAsync(id, model, CurrentAccountId, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return View(model);
        }

        TempData["Success"] = "Đã cập nhật thông tin nhân viên.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Thay doi trang thai hoat dong (Kich hoat / Ngung hoat dong) cua nhan vien.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(
        int id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var result = await _employeeService.SetActiveAsync(id, isActive, CurrentAccountId, cancellationToken);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? isActive ? "Đã mở lại nhân viên." : "Đã chuyển nhân viên sang ngừng hoạt động."
            : result.ErrorMessage;
        return RedirectToAction(nameof(Index));
    }
}
