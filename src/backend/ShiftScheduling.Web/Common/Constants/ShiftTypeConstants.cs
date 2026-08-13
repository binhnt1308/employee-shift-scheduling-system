namespace ShiftScheduling.Common.Constants;

/// <summary>
/// Dinh nghia ba ca lam viec co dinh duy nhat cua he thong theo BR03.
/// </summary>
public static class ShiftTypeConstants
{
    public const string MorningCode = "MORNING";
    public const string AfternoonCode = "AFTERNOON";
    public const string EveningCode = "EVENING";

    public static readonly TimeOnly MorningStartTime = new(8, 0);
    public static readonly TimeOnly MorningEndTime = new(12, 0);

    public static readonly TimeOnly AfternoonStartTime = new(12, 0);
    public static readonly TimeOnly AfternoonEndTime = new(16, 0);

    public static readonly TimeOnly EveningStartTime = new(16, 0);
    public static readonly TimeOnly EveningEndTime = new(20, 0);

    /// <summary>
    /// Kiem tra xem mot khung gio co phai la khung gio ca co dinh hop le cua BR03 hay khong.
    /// </summary>
    public static bool IsValidFixedShiftTime(string shiftCode, TimeOnly startTime, TimeOnly endTime)
    {
        return shiftCode.ToUpperInvariant() switch
        {
            MorningCode => startTime == MorningStartTime && endTime == MorningEndTime,
            AfternoonCode => startTime == AfternoonStartTime && endTime == AfternoonEndTime,
            EveningCode => startTime == EveningStartTime && endTime == EveningEndTime,
            _ => false
        };
    }
}
