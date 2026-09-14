namespace ShiftScheduling.Common.Time;

public static class DayOfWeekExtensions
{
    public static string ToShortVietnameseDay(this DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => "T2",
        DayOfWeek.Tuesday => "T3",
        DayOfWeek.Wednesday => "T4",
        DayOfWeek.Thursday => "T5",
        DayOfWeek.Friday => "T6",
        DayOfWeek.Saturday => "T7",
        DayOfWeek.Sunday => "CN",
        _ => dayOfWeek.ToString()
    };
}
