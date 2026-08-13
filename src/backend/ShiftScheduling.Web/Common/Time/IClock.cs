namespace ShiftScheduling.Common.Time;

public interface IClock
{
    DateTime UtcNow { get; }
    DateOnly TodayInVietnam { get; }
    DateTime ToVietnamTime(DateTime utcDateTime);
}
