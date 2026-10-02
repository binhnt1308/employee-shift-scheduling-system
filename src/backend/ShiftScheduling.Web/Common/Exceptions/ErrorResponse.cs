namespace ShiftScheduling.Models.Errors;

// Định dạng response lỗi thống nhất gồm mã lỗi, thông báo, chi tiết và TraceId.
public sealed record ErrorDetail(string? Field, string Issue);

public sealed record ErrorBody(
    string Code,
    string Message,
    IReadOnlyList<ErrorDetail> Details,
    string TraceId);

public sealed record ErrorResponse(ErrorBody Error);
