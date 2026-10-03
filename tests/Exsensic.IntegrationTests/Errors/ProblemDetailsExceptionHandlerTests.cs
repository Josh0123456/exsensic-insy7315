using System.Text.Json;
using Exsensic.Api.Errors;
using Exsensic.Contracts.Enums;
using Exsensic.Core.Bookings;
using Exsensic.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Exsensic.IntegrationTests.Errors;

/// <summary>
/// Checks that each exception becomes the right ProblemDetails response: status, code, title and
/// traceId, with no internal details leaking for unexpected errors (docs/CONTRACTS.md §7).
/// The handler runs with the same registration as the API, against an in-memory request.
/// </summary>
public class ProblemDetailsExceptionHandlerTests
{
    /// <summary>Each known exception maps to its status and error code.</summary>
    public static TheoryData<Exception, int, string> KnownExceptions() => new()
    {
        { new NotFoundException("Booking"), 404, "not_found" },
        { new InvalidBookingTransitionException(BookingStatus.Completed, BookingAction.Cancel), 409, "invalid_transition" },
        { new SlotUnavailableException(), 409, "slot_unavailable" },
        { new StaffUnavailableException(), 409, "staff_unavailable" },
        { new ConcurrencyConflictException(), 409, "concurrency_conflict" },
        { new BusinessRuleException("cancel_window_closed", "Bookings can't be cancelled within 24 hours of the start."), 409, "cancel_window_closed" },
        { new DbUpdateConcurrencyException("Stale row version."), 409, "concurrency_conflict" },
        { new RequestValidationException(new Dictionary<string, string[]> { ["Requirements[quantity]"] = ["Too many."] }), 400, "validation_failed" },
    };

    /// <summary>Known exceptions produce their status and code, plus a traceId.</summary>
    [Theory]
    [MemberData(nameof(KnownExceptions))]
    public async Task TryHandleAsync_KnownException_WritesStatusAndCode(Exception exception, int expectedStatus, string expectedCode)
    {
        var (context, body) = await HandleAsync(exception);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal(expectedStatus, body.GetProperty("status").GetInt32());
        Assert.Equal(expectedCode, body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
    }

    /// <summary>A business-rule message is written for users, so it is passed through as the title.</summary>
    [Fact]
    public async Task TryHandleAsync_BusinessRule_UsesExceptionMessageAsTitle()
    {
        var exception = new BusinessRuleException("cancel_window_closed", "Bookings can't be cancelled within 24 hours of the start.");

        var (_, body) = await HandleAsync(exception);

        Assert.Equal(exception.Message, body.GetProperty("title").GetString());
    }

    /// <summary>A Core validation failure returns the per-field errors, keyed as the Web form names its fields.</summary>
    [Fact]
    public async Task TryHandleAsync_RequestValidation_WritesFieldErrors()
    {
        var errors = new Dictionary<string, string[]> { ["Requirements[quantity]"] = ["Number of products or people must be between 1 and 500."] };

        var (_, body) = await HandleAsync(new RequestValidationException(errors));

        var messages = body.GetProperty("errors").GetProperty("Requirements[quantity]");
        Assert.Equal("Number of products or people must be between 1 and 500.", messages[0].GetString());
    }

    /// <summary>Not found always uses the generic message, so it never hints at what was looked for.</summary>
    [Fact]
    public async Task TryHandleAsync_NotFound_UsesGenericTitle()
    {
        var (_, body) = await HandleAsync(new NotFoundException("Booking"));

        Assert.Equal(ProblemDetailsExceptionHandler.NotFoundMessage, body.GetProperty("title").GetString());
    }

    /// <summary>An unexpected exception becomes 500 server_error with nothing from the exception in the response.</summary>
    [Fact]
    public async Task TryHandleAsync_UnexpectedException_Writes500WithoutDetails()
    {
        const string secret = "Server=sql-prod;Password=hunter2";

        var (context, body) = await HandleAsync(new InvalidOperationException(secret));
        var raw = body.GetRawText();

        Assert.Equal(500, context.Response.StatusCode);
        Assert.Equal("server_error", body.GetProperty("code").GetString());
        Assert.Equal(ProblemDetailsExceptionHandler.ServerErrorMessage, body.GetProperty("title").GetString());
        Assert.DoesNotContain(secret, raw);
        Assert.DoesNotContain("InvalidOperationException", raw);
    }

    /// <summary>Each status without its own exception gets the default code from the contract.</summary>
    [Theory]
    [InlineData(400, "validation_failed")]
    [InlineData(401, "unauthenticated")]
    [InlineData(403, "forbidden")]
    [InlineData(404, "not_found")]
    [InlineData(429, "rate_limited")]
    [InlineData(500, "server_error")]
    public void CodeForStatus_KnownStatus_ReturnsContractCode(int status, string expectedCode)
    {
        Assert.Equal(expectedCode, ErrorHandlingExtensions.CodeForStatus(status));
    }

    /// <summary>
    /// Runs the registered exception handler against an in-memory request and returns the parsed response body.
    /// </summary>
    private static async Task<(HttpContext Context, JsonElement Body)> HandleAsync(Exception exception)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddExsensicErrorHandling()
            .BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = HttpMethods.Put;
        context.Request.Path = "/api/v1/bookings/1/cancel";
        context.Response.Body = new MemoryStream();

        var handler = services.GetServices<IExceptionHandler>().OfType<ProblemDetailsExceptionHandler>().Single();
        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);
        Assert.True(handled);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return (context, document.RootElement.Clone());
    }
}
