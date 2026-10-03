using Exsensic.Contracts.Common;
using Exsensic.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Exsensic.Api.Errors;

/// <summary>
/// Turns every exception that escapes a controller into an RFC 9457 ProblemDetails response with the
/// right status and error code (docs/CONTRACTS.md §7). Controllers stay thin: services throw a meaningful
/// exception and this one class decides what the client sees.
/// </summary>
/// <remarks>
/// Business-rule messages are written to be shown to users, so they are passed through. Anything
/// unexpected becomes 500 server_error with a generic message: the stack trace and details only go
/// to the logs, never to the client.
/// </remarks>
// Adapted from [n]: Microsoft (2025) Handle errors in ASP.NET Core APIs. https://learn.microsoft.com/aspnet/core/fundamentals/error-handling-api
public sealed class ProblemDetailsExceptionHandler : IExceptionHandler
{
    /// <summary>The message sent for 404, so a missing record and someone else's record look the same.</summary>
    public const string NotFoundMessage = "The requested resource was not found.";

    /// <summary>The message sent for 500. The real error is in the logs, linked by the traceId.</summary>
    public const string ServerErrorMessage = "Something went wrong on our side. Please try again later.";

    /// <summary>The message sent when a record was changed by someone else since the client loaded it.</summary>
    public const string ConcurrencyMessage = "This record was changed by someone else. Please reload it and try again.";

    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<ProblemDetailsExceptionHandler> _logger;

    /// <summary>
    /// Creates the handler with the framework's ProblemDetails writer and a logger.
    /// </summary>
    public ProblemDetailsExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<ProblemDetailsExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    /// <summary>
    /// Maps the exception to a status, code and message, logs it at the right level and writes the
    /// ProblemDetails response. Always returns true, so no exception reaches the client unhandled.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="exception">The exception that escaped the pipeline.</param>
    /// <param name="cancellationToken">Cancelled if the client disconnects.</param>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, title) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            // Expected outcomes (400, 404, 409) are not faults, so they are logged without the stack trace.
            _logger.LogInformation("Request on {Method} {Path} ended with {Status} {Code}", httpContext.Request.Method, httpContext.Request.Path, status, code);
        }

        httpContext.Response.StatusCode = status;

        // Validation failures carry per-field errors, in the same shape as automatic model validation.
        ProblemDetails problemDetails = exception is RequestValidationException validation
            ? new HttpValidationProblemDetails(validation.Errors.ToDictionary(e => e.Key, e => e.Value))
            : new ProblemDetails();
        problemDetails.Status = status;
        problemDetails.Title = title;
        problemDetails.Extensions[ErrorHandlingExtensions.CodeKey] = code;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception,
        });
    }

    /// <summary>
    /// Decides the HTTP status, error code and user-facing title for an exception.
    /// </summary>
    private static (int Status, string Code, string Title) Map(Exception exception) => exception switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, ErrorCodes.NotFound, NotFoundMessage),

        RequestValidationException validation => (StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, validation.Message),

        // Covers InvalidBookingTransition, SlotUnavailable, StaffUnavailable and ConcurrencyConflict,
        // which all inherit from BusinessRuleException and carry their own code.
        BusinessRuleException rule => (StatusCodes.Status409Conflict, rule.Code, rule.Message),

        // EF Core throws this when the RowVersion sent by the client no longer matches the database.
        DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, ErrorCodes.ConcurrencyConflict, ConcurrencyMessage),

        _ => (StatusCodes.Status500InternalServerError, ErrorCodes.ServerError, ServerErrorMessage),
    };
}
