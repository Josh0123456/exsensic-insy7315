using Exsensic.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;

namespace Exsensic.Api.OpenApi;

/// <summary>
/// Describes the API for the generated OpenAPI document: its title and version, the JWT bearer scheme, which
/// operations need a signed-in user, and the error responses each operation can return. The document is built
/// from the real controllers, so it always matches the routes. It is published as docs/openapi.json, never
/// served anonymously outside Development (docs/openapi.md).
/// </summary>
public static class OpenApiExtensions
{
    /// <summary>The name of the bearer security scheme in the document.</summary>
    public const string BearerScheme = "Bearer";

    /// <summary>The name of the shared error schema (ProblemDetails with a "code" and a "traceId").</summary>
    public const string ProblemSchema = "ApiProblem";

    private const string ProblemMediaType = "application/problem+json";

    /// <summary>
    /// The 409 error codes each operation can return (docs/CONTRACTS.md section 7), keyed by controller and
    /// action. Only operations listed here document a 409, so the document never claims a conflict that
    /// can't happen. Each entry is covered by an integration test.
    /// </summary>
    private static readonly Dictionary<string, string[]> ConflictCodes = new()
    {
        ["Auth.Register"] = [ErrorCodes.Duplicate],
        ["Bookings.Create"] = [ErrorCodes.SlotUnavailable],
        ["Bookings.Reschedule"] = [ErrorCodes.SlotUnavailable, ErrorCodes.InvalidTransition, ErrorCodes.ConcurrencyConflict],
        ["Bookings.Cancel"] = [ErrorCodes.InvalidTransition, ErrorCodes.ConcurrencyConflict, ErrorCodes.CancelWindowClosed],
        ["AdminBookings.Confirm"] = [ErrorCodes.InvalidTransition, ErrorCodes.ConcurrencyConflict, ErrorCodes.StaffUnavailable],
        ["AdminBookings.Reject"] = [ErrorCodes.InvalidTransition, ErrorCodes.ConcurrencyConflict],
        ["StaffBookings.Complete"] = [ErrorCodes.InvalidTransition, ErrorCodes.ConcurrencyConflict],
        ["AdminServices.Create"] = [ErrorCodes.Duplicate],
        ["AdminServices.Update"] = [ErrorCodes.Duplicate],
        ["AdminServices.SetActive"] = [ErrorCodes.ConcurrencyConflict],
        ["AdminTimeSlots.Block"] = [ErrorCodes.SlotHasBooking, ErrorCodes.ConcurrencyConflict],
        ["AdminTimeSlots.Delete"] = [ErrorCodes.SlotHasBooking],
        ["AdminUsers.CreateStaff"] = [ErrorCodes.Duplicate],
    };

    /// <summary>
    /// The success status of actions that return a bare result (<c>IActionResult</c>), which the generator can only
    /// describe as 200. Keyed by controller and action; each status is asserted by an existing integration test.
    /// </summary>
    private static readonly Dictionary<string, (int Status, string Description)> SuccessStatuses = new()
    {
        ["AdminServices.Create"] = (201, "Created. The Location header points at the new service."),
        ["AdminServices.Update"] = (204, "No content: the service was updated."),
        ["AdminServices.SetActive"] = (204, "No content: the service was archived or restored."),
        ["AdminTimeSlots.Block"] = (204, "No content: the slot was blocked."),
        ["AdminTimeSlots.Unblock"] = (204, "No content: the slot was unblocked."),
        ["AdminTimeSlots.Delete"] = (204, "No content: the slot was deleted."),
        ["AdminUsers.CreateStaff"] = (201, "Created. The body holds the new user's id."),
        ["AdminUsers.SetActive"] = (204, "No content: the account was activated or deactivated."),
    };

    /// <summary>
    /// Registers the OpenAPI document generator with the Exsensic title, the bearer scheme, a security
    /// requirement on every operation that doesn't allow anonymous access, and the error responses.
    /// </summary>
    public static IServiceCollection AddExsensicOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Exsensic API",
                    Version = "v1",
                    Description = "Booking API for Exsensic. Errors are RFC 9457 ProblemDetails with a \"code\" and a \"traceId\" "
                        + "(docs/CONTRACTS.md section 7). Any operation can also return 500 server_error. Sign in with "
                        + "POST /api/v1/auth/login and send the token as \"Authorization: Bearer <token>\".",
                };

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "The access token from POST /api/v1/auth/login or /api/v1/auth/register.",
                };

                document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
                document.Components.Schemas[ProblemSchema] = CreateProblemSchema();

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, _) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                var secured = !metadata.OfType<IAllowAnonymous>().Any();

                if (secured)
                {
                    // The fallback policy (docs/CONTRACTS.md section 2) requires a signed-in user, so only an
                    // explicit [AllowAnonymous] makes an operation public.
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = [],
                    });

                    var authorize = metadata.OfType<IAuthorizeData>().ToArray();
                    var access = DescribeAccess(authorize);
                    if (access.Length > 0)
                    {
                        operation.Description = string.IsNullOrEmpty(operation.Description)
                            ? access
                            : $"{operation.Description}\n\n{access}";
                    }
                }

                AddErrorResponses(operation, context, metadata, secured);
                return Task.CompletedTask;
            });
        });

        return services;
    }

    /// <summary>
    /// Adds the error responses an operation can return, each as ProblemDetails with a "code": 401 when it needs
    /// a token, 403 when it needs a role or policy, 400 when it takes a body or query string, 404 when it takes
    /// an id, 409 for the codes listed in <see cref="ConflictCodes"/>, and 429 when it is rate limited.
    /// </summary>
    private static void AddErrorResponses(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, IList<object> metadata, bool secured)
    {
        var document = context.Document!;
        operation.Responses ??= [];

        if (context.Description.ActionDescriptor is ControllerActionDescriptor created
            && SuccessStatuses.TryGetValue($"{created.ControllerName}.{created.ActionName}", out var success))
        {
            operation.Responses.Remove("200");
            operation.Responses[success.Status.ToString()] = new OpenApiResponse { Description = success.Description };
        }

        if (secured)
        {
            Add(operation, document, 401, "unauthenticated: no token, or an expired or invalid one.");
        }

        if (secured && metadata.OfType<IAuthorizeData>().Any(a => a.Roles is { Length: > 0 } || a.Policy is { Length: > 0 }))
        {
            Add(operation, document, 403, "forbidden: signed in, but the wrong role for this operation.");
        }

        var hasQuery = operation.Parameters?.Any(p => p.In == ParameterLocation.Query) == true;
        if (operation.RequestBody is not null || hasQuery)
        {
            Add(operation, document, 400, "validation_failed: invalid input. The body lists the fields at fault.");
        }

        if (operation.Parameters?.Any(p => p.In == ParameterLocation.Path) == true)
        {
            Add(operation, document, 404, "not_found: it doesn't exist, or (for a booking) it isn't yours.");
        }

        if (context.Description.ActionDescriptor is ControllerActionDescriptor action
            && ConflictCodes.TryGetValue($"{action.ControllerName}.{action.ActionName}", out var codes))
        {
            Add(operation, document, 409, $"Conflict. Code: {string.Join(", ", codes)}.");
        }

        if (metadata.OfType<EnableRateLimitingAttribute>().Any())
        {
            Add(operation, document, 429, "rate_limited: too many requests. The Retry-After header says when to try again.");
        }
    }

    private static void Add(OpenApiOperation operation, OpenApiDocument document, int status, string description) =>
        operation.Responses![status.ToString()] = new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [ProblemMediaType] = new() { Schema = new OpenApiSchemaReference(ProblemSchema, document) },
            },
        };

    /// <summary>The error body: standard ProblemDetails fields plus the "code", "traceId" and per-field "errors".</summary>
    private static OpenApiSchema CreateProblemSchema() => new()
    {
        Type = JsonSchemaType.Object,
        Description = "An RFC 9457 ProblemDetails error. The \"code\" values are listed in docs/CONTRACTS.md section 7.",
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["type"] = new OpenApiSchema { Type = JsonSchemaType.String },
            ["title"] = new OpenApiSchema { Type = JsonSchemaType.String },
            ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
            ["detail"] = new OpenApiSchema { Type = JsonSchemaType.String },
            ["code"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "A stable, machine-readable error code, for example slot_unavailable.",
            },
            ["traceId"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "Quote this when reporting a problem; it links to the server log.",
            },
            ["errors"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Description = "Only on validation_failed: the messages for each field.",
                AdditionalProperties = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchema { Type = JsonSchemaType.String },
                },
            },
        },
    };

    /// <summary>A one-line "Access: ..." note naming the roles and policies an operation requires.</summary>
    private static string DescribeAccess(IEnumerable<IAuthorizeData> authorize)
    {
        var requirements = authorize
            .SelectMany(a => new[] { a.Roles is { Length: > 0 } ? $"role {a.Roles}" : null, a.Policy is { Length: > 0 } ? $"policy {a.Policy}" : null })
            .Where(text => text is not null)
            .Distinct()
            .ToArray();

        return requirements.Length == 0
            ? "Access: any signed-in user."
            : $"Access: {string.Join(", ", requirements)}.";
    }
}
