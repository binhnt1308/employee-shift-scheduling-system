using ShiftScheduling.Features.SchedulePeriods.ViewModels;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.SchedulePeriods.Services;

/// <summary>
/// Service quản lý kỳ lịch và định mức nhân sự theo FR03/UC03.
/// </summary>
public interface ISchedulePeriodService
{
    /// <summary>
    /// Lấy danh sách các kỳ lịch có phân trang.
    /// </summary>
    Task<SchedulePeriodIndexViewModel> GetIndexAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy chi tiết kỳ lịch và 21 định mức nhân sự trong tuần.
    /// </summary>
    Task<SchedulePeriodDetailsViewModel?> GetDetailsAsync(
        int schedulePeriodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy dữ liệu để chỉnh sửa 21 định mức nhân sự trong tuần.
    /// </summary>
    Task<StaffingRequirementEditViewModel?> GetEditStaffingAsync(
        int schedulePeriodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo kỳ lịch 7 ngày và khởi tạo 21 dòng định mức ca làm việc.
    /// </summary>
    Task<ServiceResult> CreateAsync(
        SchedulePeriodCreateViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật định mức nhân sự bán hàng và thu ngân cho 21 ca trong tuần.
    /// </summary>
    Task<ServiceResult> UpdateStaffingAsync(
        StaffingRequirementEditViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mở đăng ký lịch rảnh khi dữ liệu kỳ lịch và thời hạn hợp lệ.
    /// </summary>
    Task<ServiceResult> OpenRegistrationAsync(
        int schedulePeriodId,
        int actorAccountId,
        CancellationToken cancellationToken = default);
}
