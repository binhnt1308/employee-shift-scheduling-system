using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Dashboard.Services;

/// <summary>
/// Service tong hop so lieu hien thi tren Dashboard.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Lay so lieu tong hop tai khoan, nhan vien va ca lam viec de hien thi trang chu.
    /// </summary>
    /// <param name="cancellationToken">Token huy thao tac khi request ngung.</param>
    /// <returns>ViewModel chua cac con so thuc te tu database.</returns>
    Task<DashboardViewModel> GetDashboardAsync(
        int? userAccountId = null,
        CancellationToken cancellationToken = default);
}
