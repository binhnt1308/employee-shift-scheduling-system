using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.ShiftTypes.Services;

public sealed partial class ShiftTypeService
{
    private static ShiftTypeListItemViewModel MapListItem(ShiftType shiftType) => new()
    {
        ShiftTypeId = shiftType.ShiftTypeId,
        ShiftCode = shiftType.ShiftCode,
        ShiftName = shiftType.ShiftName,
        StartTime = shiftType.StartTime,
        EndTime = shiftType.EndTime,
        DurationHours = shiftType.DurationHours,
        DisplayOrder = shiftType.DisplayOrder,
        ConfigurationVersion = shiftType.ConfigurationVersion,
        IsActive = shiftType.IsActive
    };

    private static ShiftTypeEditViewModel MapEdit(ShiftType shiftType) => new()
    {
        ShiftTypeId = shiftType.ShiftTypeId,
        ShiftCode = shiftType.ShiftCode,
        ShiftName = shiftType.ShiftName,
        StartTime = shiftType.StartTime,
        EndTime = shiftType.EndTime,
        DisplayOrder = shiftType.DisplayOrder,
        IsActive = shiftType.IsActive
    };
}
