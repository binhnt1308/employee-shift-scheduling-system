using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.ShiftTypes.Services;

// Các hàm ánh xạ dữ liệu nghiệp vụ cấu hình ca làm việc sang ViewModel, tách khỏi luồng xử lý chính để dễ đọc.
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
