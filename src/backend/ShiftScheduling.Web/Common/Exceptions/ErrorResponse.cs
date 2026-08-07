namespace ShiftScheduling.Models.Errors;

public sealed record ErrorDetail(string? Field, string Issue);

public sealed record ErrorBody(
    string Code,
    string Message,
    IReadOnlyList<ErrorDetail> Details,
    string TraceId);

public sealed record ErrorResponse(ErrorBody Error);
