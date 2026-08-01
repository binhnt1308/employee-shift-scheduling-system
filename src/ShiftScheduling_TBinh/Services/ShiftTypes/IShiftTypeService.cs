using ShiftScheduling.Services.Common;
using ShiftScheduling.ViewModels.ShiftType;

namespace ShiftScheduling.Services.ShiftTypes;

public interface IShiftTypeService
{
    Task<IReadOnlyList<ShiftTypeListItemViewModel>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<ShiftTypeEditViewModel?> GetEditAsync(
        int shiftTypeId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(
        int shiftTypeId,
        ShiftTypeEditViewModel model,
        CancellationToken cancellationToken = default);
}
