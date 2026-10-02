using System.Diagnostics;
using Exsensic.Contracts.Common;

namespace Exsensic.Api.Errors;

/// <summary>
/// Registers the API's error handling, so Program.cs needs one line for the services and one for the
/// pipeline. Every error response, whether from an exception, model validation or an empty 401/403/404,
/// becomes ProblemDetails with a "code" and a "traceId" extension (docs/CONTRACTS.md §7).
/// </summary>
public static class ErrorHandlingExtensions
{
    /// <summary>The ProblemDetails extension that carries the ErrorCodes value.</summary>
    public const string CodeKey = "code";

    /// <summary>The ProblemDetails extension that links the response to the logs.</summary>
    public const string TraceIdKey = "traceId";

    /// <summary>
    /// Adds ProblemDetails with the code and traceId extensions, and the exception handler.
    /// </summary>
    /// <param name="services">The API's service collection.</param>
    public static IServiceCollection AddExsensicErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            // Runs for every ProblemDetails the framework writes, including automatic 400 validation
            // responses, so the Web can always rely on both extensions being present.
            options.CustomizeProblemDetails = context =>
            {
                var extensions = context.ProblemDetails.Extensions;

                if (!extensions.ContainsKey(CodeKey))
                {
                    extensions[CodeKey] = CodeForStatus(context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode);
                }

                extensions[TraceIdKey] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
            };
        });

        services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

        return services;
    }

    /// <summary>
    /// Adds the exception handler and turns empty error responses (for example a 401 from authentication
    /// or a 404 for an unknown route) into ProblemDetails. Call it straight after the hosting middleware,
    /// so it catches exceptions from everything that runs after it.
    /// </summary>
    /// <param name="app">The API application.</param>
    public static WebApplication UseExsensicErrorHandling(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        return app;
    }

    /// <summary>
    /// The default error code for a status when no exception supplied one (docs/CONTRACTS.md §7).
    /// </summary>
    /// <param name="status">The HTTP status code of the response.</param>
    public static string CodeForStatus(int status) => status switch
    {
        StatusCodes.Status400BadRequest => ErrorCodes.ValidationFailed,
        StatusCodes.Status401Unauthorized => ErrorCodes.Unauthenticated,
        StatusCodes.Status403Forbidden => ErrorCodes.Forbidden,
        StatusCodes.Status404NotFound => ErrorCodes.NotFound,
        StatusCodes.Status429TooManyRequests => ErrorCodes.RateLimited,
        _ => ErrorCodes.ServerError,
    };
}
