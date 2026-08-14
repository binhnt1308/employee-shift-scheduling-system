namespace ShiftScheduling.Common.Time;

public sealed class SystemClock : IClock
{
    private static readonly TimeZoneInfo VietnamTimeZone = GetVietnamTimeZone();

    public DateTime UtcNow => DateTime.UtcNow;

    public DateOnly TodayInVietnam
    {
        get
        {
            var vietnamNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
            return DateOnly.FromDateTime(vietnamNow);
        }
    }

    public DateTime ToVietnamTime(DateTime utcDateTime)
    {
        var normalizedUtc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(normalizedUtc, VietnamTimeZone);
    }

    public DateTime ToUtcFromVietnamTime(DateTime vietnamDateTime)
    {
        var unspecified = DateTime.SpecifyKind(vietnamDateTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, VietnamTimeZone);
    }

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
    }
}
