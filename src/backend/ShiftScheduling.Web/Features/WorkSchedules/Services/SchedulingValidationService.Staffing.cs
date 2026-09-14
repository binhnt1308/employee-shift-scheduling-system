using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.WorkSchedules.Services;

public sealed partial class SchedulingValidationService
{
    /// <summary>
    /// Kiểm tra định mức nhân sự theo vị trí (BR12) và yêu cầu nhân viên kinh nghiệm (BR13).
    /// </summary>
    private static void ValidateStaffingRequirements(
        IReadOnlyList<StaffingRequirement>? staffingRequirements,
        HashSet<int> fixedShiftIds,
        List<ShiftAssignment> validAssignments,
        Dictionary<int, Employee> empMap,
        Dictionary<int, ShiftType> shiftTypeMap,
        List<string> hardErrors)
    {
        if (staffingRequirements == null || staffingRequirements.Count == 0)
        {
            return;
        }

        var reqs = staffingRequirements.Where(r => fixedShiftIds.Contains(r.ShiftTypeId));

        foreach (var req in reqs)
        {
            ValidateSingleShiftStaffing(req, validAssignments, empMap, shiftTypeMap, hardErrors);
        }
    }

    /// <summary>
    /// Kiểm tra định mức vị trí và kinh nghiệm cho một ca làm việc cụ thể.
    /// </summary>
    private static void ValidateSingleShiftStaffing(
        StaffingRequirement req,
        List<ShiftAssignment> validAssignments,
        Dictionary<int, Employee> empMap,
        Dictionary<int, ShiftType> shiftTypeMap,
        List<string> hardErrors)
    {
        var shiftName = req.ShiftType?.ShiftName
            ?? (shiftTypeMap.TryGetValue(req.ShiftTypeId, out var st) ? st.ShiftName : $"Ca {req.ShiftTypeId}");
        var shiftAssigns = validAssignments
            .Where(a => a.WorkDate == req.WorkDate && a.ShiftTypeId == req.ShiftTypeId)
            .ToList();

        var messagePrefix = $"Ngày {req.WorkDate:dd/MM} {shiftName}: ";

        var salesCount = shiftAssigns.Count(
            a => empMap.TryGetValue(a.EmployeeId, out var e) && e.Position == EmployeePosition.Sales);
        var cashierCount = shiftAssigns.Count(
            a => empMap.TryGetValue(a.EmployeeId, out var e) && e.Position == EmployeePosition.Cashier);

        // BR12: Định mức nhân sự theo vị trí
        if (salesCount < req.RequiredSalesStaff)
        {
            hardErrors.Add($"{messagePrefix}Thiếu {req.RequiredSalesStaff - salesCount} nhân viên Tư vấn.");
        }
        if (salesCount > req.RequiredSalesStaff)
        {
            hardErrors.Add($"{messagePrefix}Dư {salesCount - req.RequiredSalesStaff} nhân viên Tư vấn.");
        }
        if (cashierCount < req.RequiredCashiers)
        {
            hardErrors.Add($"{messagePrefix}Thiếu {req.RequiredCashiers - cashierCount} nhân viên Thu ngân.");
        }
        if (cashierCount > req.RequiredCashiers)
        {
            hardErrors.Add($"{messagePrefix}Dư {cashierCount - req.RequiredCashiers} nhân viên Thu ngân.");
        }

        // BR13: Mỗi ca phải có ít nhất 1 nhân viên Level 2+ khi ca có yêu cầu nhân sự
        var hasLvl2Plus = shiftAssigns.Any(
            a => empMap.TryGetValue(a.EmployeeId, out var e) && e.Level >= EmployeeLevel.Level2);
        if (!hasLvl2Plus && (req.RequiredSalesStaff + req.RequiredCashiers > 0))
        {
            hardErrors.Add($"{messagePrefix}Chưa có nhân viên kinh nghiệm (Level 2 trở lên).");
        }
    }
}
