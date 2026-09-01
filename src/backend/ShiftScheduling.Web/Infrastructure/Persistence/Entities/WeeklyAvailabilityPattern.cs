using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Models.Entities;

/// <summary>
/// Mẫu lịch rảnh cố định theo tuần của nhân viên (NC-02/QT-2).
/// Lưu bằng ShiftCode thay vì ShiftTypeId để không phụ thuộc vào phiên bản cấu hình ca.
/// </summary>
public class WeeklyAvailabilityPattern
{
    public int WeeklyAvailabilityPatternId { get; set; }
    public int EmployeeId { get; set; }

    /// <summary>
    /// Thứ trong tuần: 1=Thứ Hai, 2=Thứ Ba, …, 7=Chủ Nhật.
    /// </summary>
    public int DayOfWeek { get; set; }

    /// <summary>
    /// Mã ca cố định: MORNING, AFTERNOON hoặc EVENING (BR03).
    /// Không FK vào ShiftTypes để tránh phụ thuộc vào ConfigurationVersion.
    /// </summary>
    public string ShiftCode { get; set; } = string.Empty;

    // NC-02: Mức nguyện vọng của ô trong mẫu tuần.
    public AvailabilityPreferenceLevel PreferenceLevel { get; set; } = AvailabilityPreferenceLevel.Normal;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Employee Employee { get; set; } = null!;
}
