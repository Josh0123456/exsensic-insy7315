using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exsensic.Api.OpenApi;

/// <summary>
/// Describes the API for the generated OpenAPI document: its title and version, the JWT bearer scheme, and
/// which operations need a signed-in user. The document is built from the real controllers, so it always
/// matches the routes. It is published as docs/openapi.json, never served anonymously outside Development
/// (docs/openapi.md).
/// </summary>
public static class OpenApiExtensions
{
    /// <summary>The name of the bearer security scheme in the document.</summary>
    public const string BearerScheme = "Bearer";

    /// <summary>
    /// Registers the OpenAPI document generator with the Exsensic title, the bearer scheme and a security
    /// requirement on every operation that doesn't allow anonymous access.
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
                        + "(docs/CONTRACTS.md section 7). Sign in with POST /api/v1/auth/login and send the token as "
                        + "\"Authorization: Bearer <token>\".",
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

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, _) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;

                // The fallback policy (docs/CONTRACTS.md section 2) requires a signed-in user, so only an
                // explicit [AllowAnonymous] makes an operation public.
                if (metadata.OfType<IAllowAnonymous>().Any())
                {
                    return Task.CompletedTask;
                }

                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = [],
                });

                var access = DescribeAccess(metadata.OfType<IAuthorizeData>());
                if (access.Length > 0)
                {
                    operation.Description = string.IsNullOrEmpty(operation.Description)
                        ? access
                        : $"{operation.Description}\n\n{access}";
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }

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
