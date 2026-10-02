using ShiftScheduling.Models.Results;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.ShiftTypes.Services;

public interface IShiftTypeService
{
    /// <summary>
    /// Lấy danh sách ba ca cố định theo thứ tự hiển thị.
    /// </summary>
    Task<IReadOnlyList<ShiftTypeListItemViewModel>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy cấu hình ba ca đang hoạt động để chỉnh sửa đồng thời trên một form.
    /// </summary>
    Task<ShiftConfigurationEditViewModel?> GetConfigurationAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật đồng thời cấu hình ba ca làm việc bằng một phiên bản mới.
    /// </summary>
    Task<ServiceResult> UpdateConfigurationAsync(
        ShiftConfigurationEditViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy thông tin một ca để cập nhật theo luồng cũ.
    /// </summary>
    Task<ShiftTypeEditViewModel?> GetEditAsync(
        int shiftTypeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật khung giờ ca theo luồng cũ.
    /// </summary>
    Task<ServiceResult> UpdateAsync(
        int shiftTypeId,
        ShiftTypeEditViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);
}
