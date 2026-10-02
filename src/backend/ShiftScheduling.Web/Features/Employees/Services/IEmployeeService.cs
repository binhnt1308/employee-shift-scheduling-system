using ShiftScheduling.Models.Results;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Employees.Services;

public interface IEmployeeService
{
    /// <summary>
    /// Lấy danh sách nhân viên có tìm kiếm và phân trang.
    /// </summary>
    Task<EmployeeIndexViewModel> GetIndexAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy dữ liệu nhân viên để hiển thị trên màn hình cập nhật.
    /// </summary>
    Task<EmployeeFormViewModel?> GetFormAsync(
        int employeeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo hồ sơ nhân viên sau khi kiểm tra quy tắc nghiệp vụ.
    /// </summary>
    Task<ServiceResult> CreateAsync(
        EmployeeFormViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật hồ sơ nhân viên và ghi nhật ký thao tác.
    /// </summary>
    Task<ServiceResult> UpdateAsync(
        int employeeId,
        EmployeeFormViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chuyển trạng thái hoạt động mà không xóa dữ liệu lịch sử.
    /// </summary>
    Task<ServiceResult> SetActiveAsync(
        int employeeId,
        bool isActive,
        int actorAccountId,
        CancellationToken cancellationToken = default);
}
