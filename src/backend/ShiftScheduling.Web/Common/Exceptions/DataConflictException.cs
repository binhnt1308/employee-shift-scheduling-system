namespace ShiftScheduling.Models.Errors;

// Biểu diễn xung đột dữ liệu có thể xử lý ở mức HTTP 409 thay vì lỗi hệ thống 500.
public sealed class DataConflictException : Exception
{
    public DataConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
