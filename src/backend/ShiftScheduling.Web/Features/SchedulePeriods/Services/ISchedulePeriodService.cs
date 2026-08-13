using ShiftScheduling.Features.SchedulePeriods.ViewModels;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.SchedulePeriods.Services;

/// <summary>
/// Service quan ly ky lich va dinh muc nhan su theo FR03/UC03.
/// </summary>
public interface ISchedulePeriodService
{
    /// <summary>
    /// Lay danh sach cac ky lich co phan trang.
    /// </summary>
    Task<SchedulePeriodIndexViewModel> GetIndexAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lay thong tin chi tiet ky lich va dinh muc nhan su 21 ca trong tuan.
    /// </summary>
    Task<SchedulePeriodDetailsViewModel?> GetDetailsAsync(
        int schedulePeriodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lay du lieu de chinh sua dinh muc nhan su 21 ca trong tuan.
    /// </summary>
    Task<StaffingRequirementEditViewModel?> GetEditStaffingAsync(
        int schedulePeriodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tao ky lich 7 ngay va khoi tao 21 dong dinh muc ca lam viec.
    /// </summary>
    Task<ServiceResult> CreateAsync(
        SchedulePeriodCreateViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cap nhat dinh muc nhan su ban hang va thu ngan cho 21 ca trong tuan.
    /// </summary>
    Task<ServiceResult> UpdateStaffingAsync(
        StaffingRequirementEditViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mo dang ky lich ranh cho ky lich khi du lieu va thoi han hop le.
    /// </summary>
    Task<ServiceResult> OpenRegistrationAsync(
        int schedulePeriodId,
        int actorAccountId,
        CancellationToken cancellationToken = default);
}
