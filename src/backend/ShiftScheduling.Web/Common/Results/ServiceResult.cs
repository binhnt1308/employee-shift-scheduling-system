namespace ShiftScheduling.Models.Results;

public sealed record ServiceResult(bool Succeeded, string? ErrorCode, string? ErrorMessage)
{
    public static ServiceResult Success()
    {
        return new ServiceResult(true, null, null);
    }

    public static ServiceResult Failure(string errorCode, string errorMessage)
    {
        return new ServiceResult(false, errorCode, errorMessage);
    }
}
