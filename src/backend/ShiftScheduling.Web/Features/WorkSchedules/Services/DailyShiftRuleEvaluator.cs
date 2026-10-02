using ShiftScheduling.Common.Constants;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.WorkSchedules.Services;

/// <summary>
/// Hàm hỗ trợ thuần kiểm tra quy tắc xếp ca trong ngày (BR-01):
/// số ca tối đa, ca gãy và chồng lấn thời gian.
/// </summary>
public static class DailyShiftRuleEvaluator
{
    /// <summary>
    /// Tính số ca tối đa hiệu lực theo loại hợp đồng (PT luôn tối đa 1 ca/ngày; FT theo cấu hình 1 hoặc 2).
    /// </summary>
    public static int GetEffectiveMaxShifts(EmploymentType employmentType, int policyMaxShifts)
    {
        return employmentType == EmploymentType.PartTime ? 1 : policyMaxShifts;
    }

    /// <summary>
    /// Kiểm tra hai khoảng thời gian có chồng lấn nhau hay không.
    /// </summary>
    public static bool HasTimeOverlap(TimeOnly start1, TimeOnly end1, TimeOnly start2, TimeOnly end2)
    {
        return start1 < end2 && start2 < end1;
    }

    /// <summary>
    /// Kiểm tra danh sách ca trong ngày có cặp ca nào trùng ID hoặc chồng lấn thời gian hay không.
    /// </summary>
    public static bool HasAnyShiftOverlap(IReadOnlyList<ShiftType> shifts)
    {
        for (int i = 0; i < shifts.Count; i++)
        {
            for (int j = i + 1; j < shifts.Count; j++)
            {
                if (shifts[i].ShiftTypeId == shifts[j].ShiftTypeId)
                {
                    return true;
                }
                if (HasTimeOverlap(shifts[i].StartTime, shifts[i].EndTime, shifts[j].StartTime, shifts[j].EndTime))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Kiểm tra xem tổ hợp ca có phải là ca gãy bị cấm (Sáng-Tối khi AllowSplitShift = false) hay không.
    /// </summary>
    public static bool IsForbiddenSplitShift(IReadOnlyList<ShiftType> shifts, bool allowSplitShift)
    {
        if (allowSplitShift || shifts.Count != 2)
        {
            return false;
        }

        var codes = shifts.Select(s => s.ShiftCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return codes.Contains(ShiftTypeConstants.MorningCode)
            && codes.Contains(ShiftTypeConstants.EveningCode)
            && !codes.Contains(ShiftTypeConstants.AfternoonCode);
    }

    /// <summary>
    /// Trích xuất danh sách các ca làm việc (ShiftType) duy nhất thuộc về kỳ lịch
    /// dựa trên StaffingRequirements và danh sách ShiftType đã tải.
    /// </summary>
    public static IReadOnlyList<ShiftType> ExtractPeriodShifts(
        IEnumerable<StaffingRequirement>? staffingRequirements,
        IReadOnlyList<ShiftType>? allShifts)
    {
        if (staffingRequirements == null)
        {
            return Array.Empty<ShiftType>();
        }

        var shiftMap = allShifts?
            .GroupBy(s => s.ShiftTypeId)
            .ToDictionary(g => g.Key, g => g.First()) ?? new Dictionary<int, ShiftType>();

        var distinctShifts = new Dictionary<int, ShiftType>();

        foreach (var req in staffingRequirements)
        {
            // Bước 1: Nếu req.ShiftType != null, kiểm tra ngày tính nhất quán với ShiftTypeId
            if (req.ShiftType != null && req.ShiftType.ShiftTypeId != req.ShiftTypeId)
            {
                // Navigation và FK không nhất quán -> fail-closed ngày lập tức, không fallback
                return Array.Empty<ShiftType>();
            }

            // Bước 2: Chỉ sau khi đã xác minh navigation/FK mới được kiểm tra deduplicate
            if (distinctShifts.ContainsKey(req.ShiftTypeId))
            {
                continue;
            }

            // Bước 3: Nếu chưa có ID trong dictionary
            if (req.ShiftType != null)
            {
                distinctShifts[req.ShiftTypeId] = req.ShiftType;
            }
            else if (shiftMap.TryGetValue(req.ShiftTypeId, out var matched))
            {
                distinctShifts[req.ShiftTypeId] = matched;
            }
            else
            {
                // Condition B: Mọi ShiftTypeId khác nhau xuất hiện trong StaffingRequirements
                // phải tìm thấy trong bộ ShiftType truyền vào. Nếu có ID không tìm thấy: fail-closed!
                return Array.Empty<ShiftType>();
            }
        }

        return distinctShifts.Values.ToList();
    }

    /// <summary>
    /// Xác minh và giải quyết chính sách ca làm việc (MaxShifts, AllowSplitShift) của kỳ lịch theo nguyên tắc Fail-Closed.
    /// Xác minh:
    /// 1. Có chính xác 3 ca cố định: MORNING, AFTERNOON, EVENING (không trùng mã, không thiếu mã).
    /// 2. Các ca thuộc cùng ConfigurationVersion.
    /// 3. Các ca có cùng MaxShiftsPerEmployeePerDay.
    /// 4. Các ca có cùng AllowSplitShift.
    /// 5. Max = 1 và AllowSplit = true là không nhất quán và bị từ chối.
    /// 6. Max chỉ là 1 hoặc 2.
    /// </summary>
    public static bool TryResolvePolicy(
        IReadOnlyCollection<ShiftType>? periodShifts,
        out (int MaxShifts, bool AllowSplitShift) policy,
        out string? errorMessage)
    {
        policy = default;
        errorMessage = null;

        if (periodShifts == null || periodShifts.Count == 0)
        {
            errorMessage = "Kỳ lịch không có dữ liệu ca làm việc hoặc định mức nhân sự.";
            return false;
        }

        // Condition A: Bộ ca của kỳ phải có chính xác ba ca cố định
        if (periodShifts.Count != 3)
        {
            errorMessage = $"Bộ ca làm việc của kỳ lịch phải có chính xác 3 ca cố định (hiện có {periodShifts.Count} ca).";
            return false;
        }

        // Điều kiện A: không trùng ShiftCode và phải đủ ba mã MORNING, AFTERNOON, EVENING.
        var codeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in periodShifts)
        {
            if (string.IsNullOrWhiteSpace(s.ShiftCode))
            {
                errorMessage = "Ca làm việc của kỳ lịch thiếu mã ca (ShiftCode).";
                return false;
            }

            if (!codeSet.Add(s.ShiftCode.Trim()))
            {
                errorMessage = $"Bộ ca làm việc của kỳ lịch bị trùng lặp mã ca '{s.ShiftCode}'.";
                return false;
            }
        }

        if (!codeSet.Contains(ShiftTypeConstants.MorningCode)
            || !codeSet.Contains(ShiftTypeConstants.AfternoonCode)
            || !codeSet.Contains(ShiftTypeConstants.EveningCode))
        {
            errorMessage = "Bộ ca làm việc của kỳ lịch phải chứa đầy đủ 3 ca: MORNING, AFTERNOON và EVENING.";
            return false;
        }

        var first = periodShifts.First();
        var version = first.ConfigurationVersion;
        var maxShifts = first.MaxShiftsPerEmployeePerDay;
        var allowSplit = first.AllowSplitShift;

        if (periodShifts.Any(s => s.ConfigurationVersion != version))
        {
            errorMessage = "Các ca làm việc trong kỳ lịch không thuộc cùng một phiên bản cấu hình.";
            return false;
        }

        if (periodShifts.Any(s => s.MaxShiftsPerEmployeePerDay != maxShifts))
        {
            errorMessage = "Chính sách số ca tối đa/ngày giữa các ca trong kỳ lịch không đồng nhất.";
            return false;
        }

        if (periodShifts.Any(s => s.AllowSplitShift != allowSplit))
        {
            errorMessage = "Chính sách ca gãy giữa các ca trong kỳ lịch không đồng nhất.";
            return false;
        }

        // Condition C: Max = 1 và AllowSplitShift = true là cấu hình không nhất quán và fail-closed
        if (maxShifts == 1 && allowSplit)
        {
            errorMessage = "Cấu hình ca không nhất quán: Không thể cho phép ca gãy (AllowSplitShift = true) khi số ca tối đa/ngày là 1.";
            return false;
        }

        if (maxShifts != 1 && maxShifts != 2)
        {
            errorMessage = $"Số ca tối đa trong ngày ({maxShifts}) không hợp lệ, chỉ được là 1 hoặc 2.";
            return false;
        }

        policy = (maxShifts, allowSplit);
        return true;
    }

    /// <summary>
    /// Trích xuất và giải quyết chính sách ca làm việc của kỳ từ StaffingRequirements và allShifts.
    /// </summary>
    public static bool TryResolvePeriodPolicy(
        IEnumerable<StaffingRequirement>? staffingRequirements,
        IReadOnlyList<ShiftType>? allShifts,
        out (int MaxShifts, bool AllowSplitShift) policy,
        out string? errorMessage)
    {
        policy = default;
        errorMessage = null;

        if (staffingRequirements == null)
        {
            errorMessage = "Kỳ lịch không có dữ liệu ca làm việc hoặc định mức nhân sự.";
            return false;
        }

        var shiftMap = allShifts?
            .GroupBy(s => s.ShiftTypeId)
            .ToDictionary(g => g.Key, g => g.First()) ?? new Dictionary<int, ShiftType>();

        var distinctShifts = new Dictionary<int, ShiftType>();

        foreach (var req in staffingRequirements)
        {
            // Bước 1: Nếu req.ShiftType != null, kiểm tra ngày tính nhất quán với ShiftTypeId
            if (req.ShiftType != null && req.ShiftType.ShiftTypeId != req.ShiftTypeId)
            {
                errorMessage = "Dữ liệu ca làm việc trong định mức nhân sự không nhất quán.";
                return false;
            }

            // Bước 2: Chỉ sau khi đã xác minh navigation/FK mới được kiểm tra deduplicate
            if (distinctShifts.ContainsKey(req.ShiftTypeId))
            {
                continue;
            }

            // Bước 3: Nếu chưa có ID trong dictionary
            if (req.ShiftType != null)
            {
                distinctShifts[req.ShiftTypeId] = req.ShiftType;
            }
            else if (shiftMap.TryGetValue(req.ShiftTypeId, out var matched))
            {
                distinctShifts[req.ShiftTypeId] = matched;
            }
            else
            {
                errorMessage = "Có ca làm việc trong định mức nhân sự không tồn tại trong cấu hình ca.";
                return false;
            }
        }

        return TryResolvePolicy(distinctShifts.Values.ToList(), out policy, out errorMessage);
    }

    /// <summary>
    /// Kiểm tra toàn bộ quy tắc ca trong ngày (BR-01) cho một nhân viên tại một ngày cụ thể.
    /// </summary>
    public static void ValidateDailyAssignments(
        Employee emp,
        DateOnly workDate,
        IReadOnlyList<ShiftType> dayShifts,
        int policyMaxShifts,
        bool allowSplitShift,
        List<string> errors)
    {
        // 1. Kiểm tra trùng ca hoặc chồng giờ (luôn cấm trong mọi trường hợp)
        for (int i = 0; i < dayShifts.Count; i++)
        {
            for (int j = i + 1; j < dayShifts.Count; j++)
            {
                if (dayShifts[i].ShiftTypeId == dayShifts[j].ShiftTypeId)
                {
                    errors.Add($"Nhân viên {emp.FullName} ({emp.EmployeeCode}) bị phân ca trùng thời gian (trùng ca {dayShifts[i].ShiftName}) trong ngày {workDate:dd/MM}.");
                }
                else if (HasTimeOverlap(dayShifts[i].StartTime, dayShifts[i].EndTime, dayShifts[j].StartTime, dayShifts[j].EndTime))
                {
                    errors.Add($"Nhân viên {emp.FullName} ({emp.EmployeeCode}) bị phân các ca trùng hoặc chồng thời gian trong ngày {workDate:dd/MM}.");
                }
            }
        }

        // 2. Kiểm tra số ca tối đa theo loại nhân viên
        var effectiveMax = GetEffectiveMaxShifts(emp.EmploymentType, policyMaxShifts);
        if (dayShifts.Count > effectiveMax)
        {
            if (emp.EmploymentType == EmploymentType.PartTime)
            {
                errors.Add($"Nhân viên bán thời gian (PT) {emp.FullName} ({emp.EmployeeCode}) chỉ được phân tối đa 1 ca/ngày.");
            }
            else
            {
                errors.Add($"Nhân viên FT {emp.FullName} ({emp.EmployeeCode}) chỉ được phân tối đa {effectiveMax} ca/ngày.");
            }
        }

        // 3. Kiểm tra ca gãy cho FullTime khi được xếp 2 ca
        if (emp.EmploymentType == EmploymentType.FullTime && dayShifts.Count == 2)
        {
            if (IsForbiddenSplitShift(dayShifts, allowSplitShift))
            {
                errors.Add($"Nhân viên FT {emp.FullName} ({emp.EmployeeCode}) ngày {workDate:dd/MM} không đạt tổ hợp 2 ca liên tiếp (hai ca liên tiếp: Sáng-Chiều hoặc Chiều-Tối).");
            }
        }
    }

    private static bool IsFixedShiftCode(string shiftCode)
    {
        return string.Equals(shiftCode, ShiftTypeConstants.MorningCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(shiftCode, ShiftTypeConstants.AfternoonCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(shiftCode, ShiftTypeConstants.EveningCode, StringComparison.OrdinalIgnoreCase);
    }
}
