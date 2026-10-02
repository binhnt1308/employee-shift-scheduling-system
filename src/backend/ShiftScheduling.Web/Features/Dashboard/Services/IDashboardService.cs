using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Dashboard.Services;

/// <summary>
/// Service tổng hợp số liệu hiển thị trên Dashboard.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Lấy số liệu tổng hợp tài khoản, nhân viên và ca làm việc để hiển thị trang chủ.
    /// </summary>
    /// <param name="cancellationToken">Token hủy thao tác khi request kết thúc sớm.</param>
    /// <returns>ViewModel chứa các số liệu thực tế từ cơ sở dữ liệu.</returns>
    Task<DashboardViewModel> GetDashboardAsync(
        int? userAccountId = null,
        CancellationToken cancellationToken = default);
}
