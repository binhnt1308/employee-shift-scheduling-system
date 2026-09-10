using ShiftScheduling.Common.Constants;
using ShiftScheduling.Features.SchedulePeriods.ViewModels;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Features.SchedulePeriods.Services;

public sealed partial class SchedulePeriodService
{
    private static List<ShiftType> GetFixedThreeShifts(IReadOnlyList<ShiftType> allShiftTypes)
    {
        var morning = allShiftTypes.FirstOrDefault(st =>
            st.ShiftCode.Equals(ShiftTypeConstants.MorningCode, StringComparison.OrdinalIgnoreCase));

        var afternoon = allShiftTypes.FirstOrDefault(st =>
            st.ShiftCode.Equals(ShiftTypeConstants.AfternoonCode, StringComparison.OrdinalIgnoreCase));

        var evening = allShiftTypes.FirstOrDefault(st =>
            st.ShiftCode.Equals(ShiftTypeConstants.EveningCode, StringComparison.OrdinalIgnoreCase));

        if (morning is null || afternoon is null || evening is null)
        {
            return new List<ShiftType>();
        }

        return new List<ShiftType> { morning, afternoon, evening };
    }

    private static bool IsFixedShiftCode(string? shiftCode)
    {
        if (string.IsNullOrWhiteSpace(shiftCode))
        {
            return false;
        }

        return shiftCode.Equals(ShiftTypeConstants.MorningCode, StringComparison.OrdinalIgnoreCase)
            || shiftCode.Equals(ShiftTypeConstants.AfternoonCode, StringComparison.OrdinalIgnoreCase)
            || shiftCode.Equals(ShiftTypeConstants.EveningCode, StringComparison.OrdinalIgnoreCase);
    }

    private static StaffingRequirementItemViewModel MapRequirementItem(StaffingRequirement req)
    {
        return new StaffingRequirementItemViewModel
        {
            StaffingRequirementId = req.StaffingRequirementId,
            WorkDate = req.WorkDate,
            ShiftTypeId = req.ShiftTypeId,
            ShiftCode = req.ShiftType?.ShiftCode ?? string.Empty,
            ShiftName = req.ShiftType?.ShiftName ?? string.Empty,
            StartTime = req.ShiftType?.StartTime ?? default,
            EndTime = req.ShiftType?.EndTime ?? default,
            RequiredSalesStaff = req.RequiredSalesStaff,
            RequiredCashiers = req.RequiredCashiers
        };
    }
}
