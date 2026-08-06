namespace ShiftScheduling.ViewModels.ShiftType;

public class ShiftTypeListItemViewModel
{
    public int ShiftTypeId { get; init; }
    public string ShiftCode { get; init; } = string.Empty;
    public string ShiftName { get; init; } = string.Empty;
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public decimal DurationHours { get; init; }
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; }
}
