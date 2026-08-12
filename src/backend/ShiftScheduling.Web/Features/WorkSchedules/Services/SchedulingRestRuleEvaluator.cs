using ShiftScheduling.Common.Constants;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Features.WorkSchedules.Services;

/// <summary>
/// Component đánh giá các quy tắc thời gian nghỉ của nhân viên (BR20, BR21).
/// </summary>
public static class SchedulingRestRuleEvaluator
{
    /// <summary>
    /// Kiểm tra quy tắc nghỉ 12h giữa 2 ngày liên tiếp (BR20) và 24h liên tục trong kỳ (BR21).
    /// </summary>
    /// <param name="employee">Nhân viên cần kiểm tra.</param>
    /// <param name="assignments">Danh sách ca được phân công cho nhân viên trong kỳ lịch.</param>
    /// <param name="shiftTypeMap">Dictionary tra cứu ShiftType theo ShiftTypeId.</param>
    /// <param name="weekStartDate">Ngày bắt đầu tuần của kỳ lịch.</param>
    /// <returns>Danh sách các thông báo lỗi nếu vi phạm BR20 hoặc BR21.</returns>
    public static List<string> EvaluateEmployeeRestRules(
        Employee employee,
        IReadOnlyList<ShiftAssignment> assignments,
        IReadOnlyDictionary<int, ShiftType> shiftTypeMap,
        DateOnly weekStartDate)
    {
        var errors = new List<string>();

        // BR20 / BRD BR-03: Bảo đảm đủ 12 giờ nghỉ giữa ca cuối ngày D và ca đầu ngày D+1.
        EvaluateDailyRest(employee, assignments, shiftTypeMap, errors);

        // BR21 / BRD BR-08: Bảo đảm có ít nhất 24 giờ nghỉ liên tục trong kỳ lịch 7 ngày.
        EvaluateWeeklyContinuousRest(employee, assignments, shiftTypeMap, weekStartDate, errors);

        return errors;
    }

    /// <summary>
    /// Kiểm tra khoảng nghỉ tối thiểu 12 giờ giữa ca cuối ngày D và ca đầu ngày D+1 (BR20).
    /// </summary>
    private static void EvaluateDailyRest(
        Employee employee,
        IReadOnlyList<ShiftAssignment> assignments,
        IReadOnlyDictionary<int, ShiftType> shiftTypeMap,
        List<string> errors)
    {
        var validAssignments = assignments
            .Where(a => shiftTypeMap.ContainsKey(a.ShiftTypeId))
            .ToList();

        var dayGroups = validAssignments
            .GroupBy(a => a.WorkDate)
            .OrderBy(g => g.Key)
            .ToList();

        for (int i = 0; i < dayGroups.Count - 1; i++)
        {
            var currentDay = dayGroups[i];
            var nextDay = dayGroups[i + 1];

            // Chỉ kiểm tra giữa 2 ngày làm việc liên tiếp (D và D+1)
            if (nextDay.Key != currentDay.Key.AddDays(1))
            {
                continue;
            }

            var lastShiftCurrentDay = currentDay
                .Select(a => shiftTypeMap[a.ShiftTypeId])
                .OrderByDescending(s => s.EndTime)
                .First();

            var firstShiftNextDay = nextDay
                .Select(a => shiftTypeMap[a.ShiftTypeId])
                .OrderBy(s => s.StartTime)
                .First();

            var previousEnd = currentDay.Key.ToDateTime(lastShiftCurrentDay.EndTime);
            var nextStart = nextDay.Key.ToDateTime(firstShiftNextDay.StartTime);
            var restDuration = nextStart - previousEnd;

            // BR20 / BRD BR-03: Nếu số giờ nghỉ giữa 2 ca < 12h -> Hard Error
            if (restDuration.TotalHours < SchedulingRuleConstants.MinimumDailyRestHours)
            {
                errors.Add(
                    $"BR20: Nhân viên {employee.FullName} ({employee.EmployeeCode}) không đủ " +
                    $"{SchedulingRuleConstants.MinimumDailyRestHours} giờ nghỉ giữa ca làm việc " +
                    $"ngày {currentDay.Key:dd/MM} và ca làm việc ngày {nextDay.Key:dd/MM}.");
            }
        }
    }

    /// <summary>
    /// Kiểm tra khoảng nghỉ liên tục tối thiểu 24 giờ trong kỳ lịch 7 ngày (BR21).
    /// </summary>
    private static void EvaluateWeeklyContinuousRest(
        Employee employee,
        IReadOnlyList<ShiftAssignment> assignments,
        IReadOnlyDictionary<int, ShiftType> shiftTypeMap,
        DateOnly weekStartDate,
        List<string> errors)
    {
        var periodStart = weekStartDate.ToDateTime(TimeOnly.MinValue);
        var periodEnd = periodStart.AddDays(7);

        var intervals = new List<(DateTime Start, DateTime End)>();
        foreach (var assignment in assignments)
        {
            if (!shiftTypeMap.TryGetValue(assignment.ShiftTypeId, out var shiftType))
            {
                continue;
            }

            var start = assignment.WorkDate.ToDateTime(shiftType.StartTime);
            var end = assignment.WorkDate.ToDateTime(shiftType.EndTime);
            if (end <= start)
            {
                end = end.AddDays(1);
            }

            intervals.Add((start, end));
        }

        if (intervals.Count == 0)
        {
            return;
        }

        intervals = intervals.OrderBy(i => i.Start).ToList();

        var has24hRestGap = HasSufficientContinuousRestGap(periodStart, periodEnd, intervals);

        // BR21 / BRD BR-08: Nếu tất cả các khoảng nghỉ đều < 24 giờ -> Hard Error
        if (!has24hRestGap)
        {
            errors.Add(
                $"BR21: Nhân viên {employee.FullName} ({employee.EmployeeCode}) không có ít nhất " +
                $"{SchedulingRuleConstants.MinimumWeeklyContinuousRestHours} giờ nghỉ liên tục trong kỳ lịch.");
        }
    }

    /// <summary>
    /// Tính toán các khoảng nghỉ giữa các ca và biên kỳ để kiểm tra có khoảng nghỉ >= 24h hay không.
    /// </summary>
    private static bool HasSufficientContinuousRestGap(
        DateTime periodStart,
        DateTime periodEnd,
        List<(DateTime Start, DateTime End)> intervals)
    {
        var minRestHours = SchedulingRuleConstants.MinimumWeeklyContinuousRestHours;

        // 1. Khoảng nghỉ từ đầu kỳ đến ca đầu tiên
        if ((intervals[0].Start - periodStart).TotalHours >= minRestHours)
        {
            return true;
        }

        // 2. Khoảng nghỉ giữa các ca kế tiếp
        for (int i = 0; i < intervals.Count - 1; i++)
        {
            var gap = intervals[i + 1].Start - intervals[i].End;
            if (gap.TotalHours >= minRestHours)
            {
                return true;
            }
        }

        // 3. Khoảng nghỉ từ ca cuối cùng đến cuối kỳ
        if ((periodEnd - intervals[^1].End).TotalHours >= minRestHours)
        {
            return true;
        }

        return false;
    }
}
