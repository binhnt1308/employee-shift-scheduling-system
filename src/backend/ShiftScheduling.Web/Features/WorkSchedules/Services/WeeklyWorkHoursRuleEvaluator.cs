using ShiftScheduling.Common.Constants;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.WorkSchedules.Services;

/// <summary>
/// Component đánh giá quy tắc giới hạn tổng giờ làm việc trong tuần theo loại hợp đồng (BR-05 / NC-01).
/// </summary>
public static class WeeklyWorkHoursRuleEvaluator
{
    /// <summary>
    /// Lấy trần tổng giờ làm việc tối đa trong tuần theo loại hợp đồng (BR-05).
    /// </summary>
    public static decimal GetMaxWeeklyHours(EmploymentType employmentType)
    {
        return employmentType == EmploymentType.FullTime
            ? SchedulingRuleConstants.FullTimeMaxWeeklyWorkHours
            : SchedulingRuleConstants.PartTimeMaxWeeklyWorkHours;
    }

    /// <summary>
    /// Tính tổng thời lượng giờ làm thực tế từ danh sách ca phân công và bản đồ ShiftType.
    /// Chỉ tính các ca nằm trong phạm vi ngày của kỳ lịch (nếu có truyền vào).
    /// </summary>
    public static decimal CalculateTotalHours(
        IEnumerable<ShiftAssignment> assignments,
        IReadOnlyDictionary<int, ShiftType> shiftTypeMap,
        DateOnly? weekStartDate = null,
        DateOnly? weekEndDate = null)
    {
        decimal total = 0m;
        foreach (var assignment in assignments)
        {
            if (weekStartDate.HasValue && assignment.WorkDate < weekStartDate.Value)
            {
                continue;
            }

            if (weekEndDate.HasValue && assignment.WorkDate > weekEndDate.Value)
            {
                continue;
            }

            if (shiftTypeMap.TryGetValue(assignment.ShiftTypeId, out var shiftType))
            {
                total += shiftType.DurationHours;
            }
            else if (assignment.ShiftType is not null)
            {
                total += assignment.ShiftType.DurationHours;
            }
        }

        return total;
    }

    /// <summary>
    /// Đánh giá trần tổng giờ làm việc trong tuần của nhân viên (BR-05).
    /// </summary>
    /// <param name="employee">Nhân viên cần kiểm tra.</param>
    /// <param name="assignments">Danh sách ca phân công của nhân viên.</param>
    /// <param name="shiftTypeMap">Bản đồ tra cứu ShiftType.</param>
    /// <param name="weekStartDate">Ngày bắt đầu kỳ lịch (tùy chọn để lọc ca đúng kỳ).</param>
    /// <param name="weekEndDate">Ngày kết thúc kỳ lịch (tùy chọn để lọc ca đúng kỳ).</param>
    /// <returns>Danh sách lỗi nếu tổng giờ làm vượt quá trần quy định theo loại hợp đồng.</returns>
    public static List<string> EvaluateWeeklyWorkHours(
        Employee employee,
        IReadOnlyList<ShiftAssignment> assignments,
        IReadOnlyDictionary<int, ShiftType> shiftTypeMap,
        DateOnly? weekStartDate = null,
        DateOnly? weekEndDate = null)
    {
        var errors = new List<string>();
        var totalHours = CalculateTotalHours(assignments, shiftTypeMap, weekStartDate, weekEndDate);
        var maxHours = GetMaxWeeklyHours(employee.EmploymentType);

        // BR-05: Tổng giờ làm trong tuần không được vượt trần theo loại hợp đồng.
        if (totalHours > maxHours)
        {
            var typeLabel = employee.EmploymentType == EmploymentType.FullTime ? "FT" : "PT";
            errors.Add(
                $"BR-05: Nhân viên {typeLabel} {employee.FullName} ({employee.EmployeeCode}) " +
                $"có tổng giờ làm trong tuần ({totalHours:0.##}h) vượt quá trần quy định ({maxHours:0.##}h).");
        }

        return errors;
    }
}
