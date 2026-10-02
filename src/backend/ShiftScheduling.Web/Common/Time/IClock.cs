namespace ShiftScheduling.Common.Time;

// Giao diện cung cấp thời gian giúp nghiệp vụ dùng thống nhất một nguồn thời gian,
// đồng thời giúp kiểm thử không phụ thuộc đồng hồ thật.
public interface IClock
{
    DateTime UtcNow { get; }
    DateOnly TodayInVietnam { get; }
    DateTime ToVietnamTime(DateTime utcDateTime);
    DateTime ToUtcFromVietnamTime(DateTime vietnamDateTime);
}
