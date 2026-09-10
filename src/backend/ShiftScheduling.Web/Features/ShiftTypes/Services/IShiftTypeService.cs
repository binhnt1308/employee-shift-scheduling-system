using ShiftScheduling.Models.Results;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.ShiftTypes.Services;

public interface IShiftTypeService
{
    /// <summary>
    /// Lay danh sach ba ca co dinh theo thu tu hien thi.
    /// </summary>
    Task<IReadOnlyList<ShiftTypeListItemViewModel>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lay cau hinh ba ca dang active de chinh sua dong thoi tren mot form.
    /// </summary>
    Task<ShiftConfigurationEditViewModel?> GetConfigurationAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cap nhat cau hinh ba ca lam viec dong thoi theo phien ban moi (versioned).
    /// </summary>
    Task<ServiceResult> UpdateConfigurationAsync(
        ShiftConfigurationEditViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lay thong tin mot ca de cap nhat (legacy).
    /// </summary>
    Task<ShiftTypeEditViewModel?> GetEditAsync(
        int shiftTypeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cap nhat khung gio ca (legacy).
    /// </summary>
    Task<ServiceResult> UpdateAsync(
        int shiftTypeId,
        ShiftTypeEditViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);
}
