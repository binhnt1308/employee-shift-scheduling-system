namespace ShiftScheduling.Services.Common;

public sealed record ServiceResult(bool Succeeded, string? ErrorMessage = null)
{
    public static ServiceResult Success() => new(true);
    public static ServiceResult Failure(string message) => new(false, message);
}
