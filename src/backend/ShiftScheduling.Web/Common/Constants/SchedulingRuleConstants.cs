namespace ShiftScheduling.Common.Constants;

/// <summary>
/// Hằng số quy định các ngưỡng và định mức trong lập lịch làm việc.
/// </summary>
public static class SchedulingRuleConstants
{
    /// <summary>
    /// Định mức số ca làm việc mục tiêu trong tuần đối với nhân viên bán thời gian (BR18).
    /// </summary>
    public const int PartTimeTargetShiftsPerWeek = 5;

    /// <summary>
    /// Số ngày làm việc tối đa trong tuần đối với nhân viên toàn thời gian (BR11, BR18).
    /// </summary>
    public const int FullTimeMaxWorkDaysPerWeek = 6;

    /// <summary>
    /// Trần tổng giờ làm việc tối đa trong tuần đối với nhân viên bán thời gian (BR-05 / NC-01).
    /// </summary>
    public const decimal PartTimeMaxWeeklyWorkHours = 28m;

    /// <summary>
    /// Trần tổng giờ làm việc tối đa trong tuần đối với nhân viên toàn thời gian (BR-05 / NC-01).
    /// </summary>
    public const decimal FullTimeMaxWeeklyWorkHours = 48m;

    /// <summary>
    /// Số giờ nghỉ tối thiểu giữa ca làm việc cuối cùng của một ngày
    /// và ca làm việc đầu tiên của ngày kế tiếp (BR20).
    /// </summary>
    public const int MinimumDailyRestHours = 12;

    /// <summary>
    /// Số giờ nghỉ liên tục tối thiểu trong kỳ lịch 7 ngày (BR21).
    /// </summary>
    public const int MinimumWeeklyContinuousRestHours = 24;
}
