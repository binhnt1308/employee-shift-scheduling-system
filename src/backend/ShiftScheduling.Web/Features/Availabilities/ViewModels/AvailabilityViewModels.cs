using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Availabilities.ViewModels;

public sealed class AvailabilityPeriodItemViewModel
{
    public int SchedulePeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public DateTime AvailabilityDeadline { get; set; }
    public RegistrationStatus RegistrationStatus { get; set; }
    public bool IsOpen { get; set; }
    public bool IsExpired { get; set; }
    public int RegisteredCount { get; set; }
}

public sealed class AvailabilityCellViewModel
{
    public DateOnly WorkDate { get; set; }
    public int ShiftTypeId { get; set; }
    public string ShiftCode { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsSelected { get; set; }

    // NC-02: Mức nguyện vọng cho ca rảnh đã chọn.
    public AvailabilityPreferenceLevel PreferenceLevel { get; set; } = AvailabilityPreferenceLevel.Normal;
}

public sealed class AvailabilityDayViewModel
{
    public DateOnly WorkDate { get; set; }
    public string DayOfWeekName { get; set; } = string.Empty;
    public List<AvailabilityCellViewModel> Shifts { get; set; } = new();
}

public sealed class AvailabilityRegisterViewModel
{
    public int SchedulePeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public DateTime AvailabilityDeadline { get; set; }
    public bool IsEditable { get; set; }
    public string? ReadonlyReason { get; set; }
    public List<AvailabilityDayViewModel> Days { get; set; } = new();
    public List<string> SelectedShiftKeys { get; set; } = new();

    // NC-02: Mẫu lịch rảnh tuần đã lưu, dùng để render nút "Áp dụng mẫu tuần".
    public WeeklyPatternViewModel WeeklyPattern { get; set; } = new();
}

// ── ViewModel tổng quan dành cho Quản lý ───────────────────────────────────

public sealed class ManagerAvailabilityCellViewModel
{
    public DateOnly WorkDate { get; set; }
    public int ShiftTypeId { get; set; }
    public string ShiftCode { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public List<ManagerAvailabilityEmployeeItem> AvailableEmployees { get; set; } = new();
}

public sealed class ManagerAvailabilityEmployeeItem
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public EmployeePosition Position { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public EmployeeLevel Level { get; set; }

    // NC-02: Quản lý cần phân biệt mức nguyện vọng Normal và Preferred.
    public AvailabilityPreferenceLevel PreferenceLevel { get; set; } = AvailabilityPreferenceLevel.Normal;
}

public sealed class ManagerAvailabilityOverviewViewModel
{
    public int SchedulePeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public int? EmployeeIdFilter { get; set; }
    public EmployeePosition? PositionFilter { get; set; }
    public DateOnly? DateFilter { get; set; }
    public int? ShiftTypeIdFilter { get; set; }
    public List<SchedulePeriodOptionViewModel> PeriodOptions { get; set; } = new();
    public List<EmployeeOptionViewModel> EmployeeOptions { get; set; } = new();
    public List<ShiftTypeOptionViewModel> ShiftOptions { get; set; } = new();
    public List<AvailabilityDayOverviewViewModel> Days { get; set; } = new();
}

public sealed class AvailabilityDayOverviewViewModel
{
    public DateOnly WorkDate { get; set; }
    public string DayOfWeekName { get; set; } = string.Empty;
    public List<ManagerAvailabilityCellViewModel> Shifts { get; set; } = new();
}

public sealed class SchedulePeriodOptionViewModel
{
    public int SchedulePeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
}

public sealed class EmployeeOptionViewModel
{
    public int EmployeeId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class ShiftTypeOptionViewModel
{
    public int ShiftTypeId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
}

// ── Weekly Pattern ViewModels (NC-02) ─────────────────────────────────────

/// <summary>Mẫu lịch rảnh tuần của nhân viên, dùng trong form Register.</summary>
public sealed class WeeklyPatternViewModel
{
    public List<WeeklyPatternCellViewModel> Cells { get; set; } = new();

    /// <summary>
    /// Ánh xạ ShiftCode viết hoa sang ShiftTypeId đang hoạt động hiện tại.
    /// Dùng bởi JavaScript khi áp dụng mẫu vào form kỳ lịch.
    /// </summary>
    public Dictionary<string, int> ShiftCodeToShiftTypeId { get; set; } = new();
}

public sealed class WeeklyPatternCellViewModel
{
    /// <summary>1=Thứ Hai … 7=Chủ Nhật.</summary>
    public int DayOfWeek { get; set; }
    public string ShiftCode { get; set; } = string.Empty;
    public int ShiftTypeId { get; set; }
    public AvailabilityPreferenceLevel PreferenceLevel { get; set; } = AvailabilityPreferenceLevel.Normal;
}

/// <summary>Dữ liệu đầu vào cho POST SaveWeeklyPattern; SE-04 yêu cầu kiểm tra lại toàn bộ ở phía server.</summary>
public sealed class WeeklyPatternEntryInputViewModel
{
    /// <summary>1=Thứ Hai … 7=Chủ Nhật.</summary>
    public int DayOfWeek { get; set; }
    public string ShiftCode { get; set; } = string.Empty;
    public AvailabilityPreferenceLevel PreferenceLevel { get; set; }
}
