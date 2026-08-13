using Microsoft.AspNetCore.Diagnostics;

namespace ShiftScheduling.Models.Errors;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;
        _logger.LogError(
            exception,
            "Unhandled error. TraceId: {TraceId}; Path: {Path}",
            traceId,
            httpContext.Request.Path);

        var isConflict = exception is DataConflictException;
        var statusCode = isConflict
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status500InternalServerError;
        var response = new ErrorResponse(new ErrorBody(
            isConflict ? ErrorCodes.DataConflict : "UNEXPECTED_ERROR",
            isConflict
                ? "Dữ liệu vừa được thay đổi hoặc bị trùng. Vui lòng tải lại trang và thử lại."
                : "Không thể hoàn tất yêu cầu. Vui lòng liên hệ quản trị viên nếu lỗi lặp lại.",
            Array.Empty<ErrorDetail>(),
            traceId));

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }
}
