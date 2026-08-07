namespace ShiftScheduling.Models.Errors;

public sealed class DataConflictException : Exception
{
    public DataConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
