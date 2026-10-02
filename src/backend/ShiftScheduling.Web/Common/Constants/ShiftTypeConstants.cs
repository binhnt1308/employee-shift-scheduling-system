namespace ShiftScheduling.Common.Constants;

/// <summary>
/// Định nghĩa ba ca làm việc cố định duy nhất của hệ thống theo BR03.
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
    /// Kiểm tra mã ca có phải là một trong ba ca cố định hay không.
    /// </summary>
    public static bool IsFixedShiftCode(string? shiftCode)
    {
        if (string.IsNullOrWhiteSpace(shiftCode))
        {
            return false;
        }

        return shiftCode.Equals(MorningCode, StringComparison.OrdinalIgnoreCase)
            || shiftCode.Equals(AfternoonCode, StringComparison.OrdinalIgnoreCase)
            || shiftCode.Equals(EveningCode, StringComparison.OrdinalIgnoreCase);
    }
}
