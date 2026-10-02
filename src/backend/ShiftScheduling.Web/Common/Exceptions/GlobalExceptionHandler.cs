using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace ShiftScheduling.Models.Errors;

/// <summary>
/// Chuyển exception chưa được xử lý thành response thống nhất, đồng thời che chi tiết nội bộ khỏi người dùng.
/// Xung đột dữ liệu trả HTTP 409; lỗi ngoài dự kiến trả HTTP 500 kèm TraceId để tra log.
/// </summary>
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

        // Tách lỗi cạnh tranh cập nhật/xung đột dữ liệu khỏi lỗi hệ thống để UI có thể yêu cầu người dùng tải lại.
        var isConcurrency = exception is DbUpdateConcurrencyException
            || (exception is DataConflictException conflict
                && conflict.InnerException is DbUpdateConcurrencyException);
        var isConflict = exception is DataConflictException || isConcurrency;
        var statusCode = isConflict
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status500InternalServerError;
        var errorCode = isConcurrency
            ? ErrorCodes.ConcurrencyConflict
            : isConflict ? ErrorCodes.DataConflict : "UNEXPECTED_ERROR";
        var message = isConcurrency
            ? "Dữ liệu đã được thay đổi bởi người dùng khác. Vui lòng tải lại và thử lại."
            : isConflict
                ? "Dữ liệu vừa được thay đổi hoặc bị trùng."
                    + " Vui lòng tải lại trang và thử lại."
                : "Không thể hoàn tất yêu cầu."
                    + " Vui lòng liên hệ quản trị viên nếu lỗi lặp lại.";

        var response = new ErrorResponse(new ErrorBody(
            errorCode,
            message,
            Array.Empty<ErrorDetail>(),
            traceId));

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }
}
