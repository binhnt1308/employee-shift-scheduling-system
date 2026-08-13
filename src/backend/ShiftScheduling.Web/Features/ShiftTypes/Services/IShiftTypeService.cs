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
    /// Lay thong tin mot ca de cap nhat.
    /// </summary>
    Task<ShiftTypeEditViewModel?> GetEditAsync(
        int shiftTypeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cap nhat khung gio ca va ngan trung gio giua cac ca dang hoat dong.
    /// </summary>
    Task<ServiceResult> UpdateAsync(
        int shiftTypeId,
        ShiftTypeEditViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);
}
