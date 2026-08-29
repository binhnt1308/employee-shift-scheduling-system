using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Models.Errors;

namespace ShiftScheduling.Tests.Common;

public class GlobalExceptionHandlerTests
{
    [Fact(DisplayName = "TryHandleAsync - DbUpdateConcurrencyException returns 409 and ConcurrencyConflict")]
    public async Task TryHandleAsync_DbUpdateConcurrencyException_Returns409WithConcurrencyError()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        var stream = new MemoryStream();
        context.Response.Body = stream;

        var exception = new DbUpdateConcurrencyException("Concurrency error");
        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);

        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var responseText = await reader.ReadToEndAsync();
        Assert.Contains(ErrorCodes.ConcurrencyConflict, responseText);
    }
}
