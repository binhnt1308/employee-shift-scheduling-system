namespace ShiftScheduling.Models.Results;

public sealed record ServiceResult(bool Succeeded, string? ErrorCode, string? ErrorMessage, string? SuccessMessage = null)
{
    public static ServiceResult Success(string? successMessage = null)
    {
        return new ServiceResult(true, null, null, successMessage);
    }

    public static ServiceResult Failure(string errorCode, string errorMessage)
    {
        return new ServiceResult(false, errorCode, errorMessage);
    }
}
