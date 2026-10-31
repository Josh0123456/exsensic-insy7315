using System.Net;
using System.Text.Json.Nodes;
using Exsensic.Contracts.Auth;
using Exsensic.IntegrationTests.Bookings;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Exsensic.IntegrationTests.OpenApi;

/// <summary>
/// The published API description (docs/openapi.json) must match the real API (docs/openapi.md).
/// The spec is generated from the running app, compared with the real routes and with the committed file,
/// and checked for anything it must not contain. No deployed API serves it anonymously, so the live
/// endpoint is checked to be absent outside Development.
/// </summary>
public sealed class OpenApiSpecTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    /// <summary>Set to "true" to rewrite docs/openapi.json from the running app instead of comparing with it.</summary>
    private const string UpdateVariable = "EXSENSIC_UPDATE_OPENAPI";

    private static readonly string[] HttpMethods = ["get", "put", "post", "delete", "patch"];

    [Fact]
    public async Task Spec_ComparedToTheRealRoutes_ListsEveryRouteAndNoOthers()
    {
        var spec = await GenerateSpecAsync();

        var documented = SpecOperations(spec);
        var real = RealApiOperations();

        Assert.Empty(real.Except(documented));
        Assert.Empty(documented.Except(real));
        Assert.NotEmpty(real);
    }

    [Fact]
    public async Task Spec_ComparedToTheAuthorisationRules_RequiresATokenExceptOnAnonymousRoutes()
    {
        var spec = await GenerateSpecAsync();

        // The anonymous routes from docs/CONTRACTS.md section 6: nothing else may be public.
        var expectedAnonymous = new[]
        {
            "POST /api/v1/auth/register",
            "POST /api/v1/auth/login",
            "GET /api/v1/services",
            "GET /api/v1/services/{id}",
            "GET /api/v1/services/{id}/availability",
            "GET /api/v1/services/{serviceId}/requirement-template",
        };

        var anonymous = new List<string>();
        foreach (var (key, operation) in Operations(spec))
        {
            var security = operation["security"] as JsonArray;
            if (security is null || security.Count == 0)
            {
                anonymous.Add(key);
            }
            else
            {
                Assert.NotNull(security[0]!["Bearer"]);
            }
        }

        Assert.Equal(expectedAnonymous.Order(), anonymous.Order());
    }

    [Fact]
    public async Task Spec_ComparedToTheAdminRoutes_NeverMarksAnAdminOperationAsAnonymous()
    {
        var spec = await GenerateSpecAsync();

        var adminOperations = Operations(spec).Where(o => o.Key.Contains("/api/v1/admin/")).ToList();

        Assert.NotEmpty(adminOperations);
        Assert.All(adminOperations, o =>
        {
            Assert.True((o.Value["security"] as JsonArray)?.Count > 0, $"{o.Key} must require a token.");
            Assert.Contains("Admin", o.Value["description"]!.GetValue<string>());
        });
    }

    [Fact]
    public async Task Spec_ComparedToTheConfiguration_ContainsNoSecretsOrHostNames()
    {
        var spec = await GenerateSpecAsync();
        var text = spec.ToJsonString();
        var configuration = api.Services.GetRequiredService<IConfiguration>();

        Assert.DoesNotContain(configuration["Jwt:SigningKey"]!, text);
        Assert.DoesNotContain(configuration["ConnectionStrings:Default"]!, text);
        Assert.DoesNotContain("Password=", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Server=", text, StringComparison.OrdinalIgnoreCase);

        // No server list: the spec doesn't say where the API is hosted, so it can't point at the wrong place
        // or reveal the web app's IP restrictions.
        Assert.Null(spec["servers"]);
    }

    [Fact]
    public async Task Spec_ComparedToTheCommittedFile_IsUpToDate()
    {
        var generated = (await GenerateSpecAsync()).ToJsonString(Indented);
        var path = Path.Combine(SolutionRoot(), "docs", "openapi.json");

        if (string.Equals(Environment.GetEnvironmentVariable(UpdateVariable), "true", StringComparison.OrdinalIgnoreCase))
        {
            await File.WriteAllTextAsync(path, generated + "\n");
            return;
        }

        Assert.True(File.Exists(path), $"docs/openapi.json is missing. Run the tests with {UpdateVariable}=true to create it.");
        var committed = JsonNode.Parse(await File.ReadAllTextAsync(path))!.ToJsonString(Indented);

        Assert.True(
            committed == generated,
            $"docs/openapi.json is out of date. Run the OpenApi tests with {UpdateVariable}=true, then commit the file.");
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/swagger/v1/swagger.json")]
    [InlineData("/openapi")]
    public async Task LiveSpecEndpoint_OutsideDevelopment_IsNotServed(string path)
    {
        // Anonymous callers are refused by the fallback policy; even a signed-in admin finds nothing there.
        var anonymous = await api.CreateAnonymousClient().GetAsync(path);
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var asAdmin = await admin.GetAsync(path);

        Assert.Contains(anonymous.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.NotFound });
        Assert.Equal(HttpStatusCode.NotFound, asAdmin.StatusCode);
    }

    private static readonly System.Text.Json.JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>Builds the document from the running app, the way the live endpoint would, without a request.</summary>
    private async Task<JsonObject> GenerateSpecAsync()
    {
        var provider = api.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");
        var document = await provider.GetOpenApiDocumentAsync(CancellationToken.None);

        return (JsonObject)JsonNode.Parse(await document.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_0))!;
    }

    /// <summary>"METHOD /path" for every operation in the spec.</summary>
    private static HashSet<string> SpecOperations(JsonObject spec) => Operations(spec).Select(o => o.Key).ToHashSet();

    private static IEnumerable<KeyValuePair<string, JsonObject>> Operations(JsonObject spec)
    {
        foreach (var (path, item) in (JsonObject)spec["paths"]!)
        {
            foreach (var (method, operation) in (JsonObject)item!)
            {
                if (HttpMethods.Contains(method))
                {
                    yield return new($"{method.ToUpperInvariant()} {path}", (JsonObject)operation!);
                }
            }
        }
    }

    /// <summary>"METHOD /path" for every controller action under /api, from the app's own routing table.</summary>
    private HashSet<string> RealApiOperations() =>
        api.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []).Select(method =>
                $"{method} /{Normalise(e.RoutePattern.RawText!)}"))
            .ToHashSet();

    /// <summary>Route constraints such as {id:int} are written {id} in OpenAPI.</summary>
    private static string Normalise(string route) =>
        System.Text.RegularExpressions.Regex.Replace(route, @"\{(\w+)(:[^}]*)?\}", "{$1}").Trim('/');

    private static string SolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Exsensic.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find Exsensic.sln above the test output folder.");
    }
}
