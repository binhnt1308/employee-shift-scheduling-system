using ShiftScheduling.Models.Results;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Employees.Services;

public interface IEmployeeService
{
    /// <summary>
    /// Lay danh sach nhan vien co tim kiem va phan trang.
    /// </summary>
    Task<EmployeeIndexViewModel> GetIndexAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lay du lieu nhan vien de hien thi tren man hinh cap nhat.
    /// </summary>
    Task<EmployeeFormViewModel?> GetFormAsync(
        int employeeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tao ho so nhan vien sau khi kiem tra quy tac nghiep vu.
    /// </summary>
    Task<ServiceResult> CreateAsync(
        EmployeeFormViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cap nhat ho so nhan vien va ghi nhat ky thao tac.
    /// </summary>
    Task<ServiceResult> UpdateAsync(
        int employeeId,
        EmployeeFormViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chuyen trang thai hoat dong ma khong xoa du lieu lich su.
    /// </summary>
    Task<ServiceResult> SetActiveAsync(
        int employeeId,
        bool isActive,
        int actorAccountId,
        CancellationToken cancellationToken = default);
}
