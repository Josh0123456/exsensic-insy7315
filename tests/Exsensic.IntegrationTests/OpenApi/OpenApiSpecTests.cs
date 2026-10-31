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

    [Fact]
    public async Task Spec_ErrorResponses_AllUseTheSharedProblemSchema()
    {
        var spec = await GenerateSpecAsync();

        var problem = spec["components"]!["schemas"]!["ApiProblem"]!["properties"]!.AsObject();
        Assert.NotNull(problem["code"]);
        Assert.NotNull(problem["traceId"]);

        var errorResponses = Operations(spec)
            .SelectMany(o => ((JsonObject)o.Value["responses"]!).Where(r => !r.Key.StartsWith('2')).Select(r => (o.Key, r.Key, Response: (JsonObject)r.Value!)))
            .ToList();
        Assert.NotEmpty(errorResponses);
        Assert.All(errorResponses, e =>
            Assert.Equal("#/components/schemas/ApiProblem", e.Response["content"]!["application/problem+json"]!["schema"]!["$ref"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Spec_SecuredOperations_Document401AndOnlyThose()
    {
        var spec = await GenerateSpecAsync();

        Assert.All(Operations(spec), o =>
        {
            var secured = (o.Value["security"] as JsonArray)?.Count > 0;
            Assert.Equal(secured, HasResponse(o.Value, "401"));
        });
    }

    [Fact]
    public async Task Spec_RoleRestrictedOperations_Document403AndOnlyThose()
    {
        var spec = await GenerateSpecAsync();

        Assert.All(Operations(spec), o =>
        {
            var access = o.Value["description"]?.GetValue<string>() ?? string.Empty;
            var restricted = access.Contains("Access: role", StringComparison.Ordinal) || access.Contains("Access: policy", StringComparison.Ordinal);
            Assert.True(restricted == HasResponse(o.Value, "403"), $"{o.Key}: 403 must be documented exactly when a role or policy is required.");
        });
    }

    [Fact]
    public async Task Spec_RateLimitedOperations_Document429AndOnlyThose()
    {
        var spec = await GenerateSpecAsync();

        var documented = Operations(spec).Where(o => HasResponse(o.Value, "429")).Select(o => o.Key).Order().ToArray();

        // The three policies in docs/CONTRACTS.md section 2.
        Assert.Equal(["POST /api/v1/auth/login", "POST /api/v1/auth/register", "POST /api/v1/bookings"], documented);
    }

    [Fact]
    public async Task Spec_ConflictResponses_ListTheCodesEachOperationCanReturn()
    {
        var spec = await GenerateSpecAsync();

        var expected = new Dictionary<string, string[]>
        {
            ["POST /api/v1/auth/register"] = ["duplicate"],
            ["POST /api/v1/bookings"] = ["slot_unavailable"],
            ["PUT /api/v1/bookings/{id}/reschedule"] = ["slot_unavailable", "invalid_transition", "concurrency_conflict"],
            ["PUT /api/v1/bookings/{id}/cancel"] = ["invalid_transition", "concurrency_conflict", "cancel_window_closed"],
            ["PUT /api/v1/admin/bookings/{id}/confirm"] = ["invalid_transition", "concurrency_conflict", "staff_unavailable"],
            ["PUT /api/v1/admin/bookings/{id}/reject"] = ["invalid_transition", "concurrency_conflict"],
            ["PUT /api/v1/staff/bookings/{id}/complete"] = ["invalid_transition", "concurrency_conflict"],
            ["POST /api/v1/admin/services"] = ["duplicate"],
            ["PUT /api/v1/admin/services/{id}"] = ["duplicate"],
            ["PUT /api/v1/admin/services/{id}/active"] = ["concurrency_conflict"],
            ["PUT /api/v1/admin/timeslots/{id}/block"] = ["slot_has_booking", "concurrency_conflict"],
            ["DELETE /api/v1/admin/timeslots/{id}"] = ["slot_has_booking"],
            ["POST /api/v1/admin/users"] = ["duplicate"],
        };

        var documented = Operations(spec).Where(o => HasResponse(o.Value, "409")).ToDictionary(o => o.Key, o => o.Value);
        Assert.Equal(expected.Keys.Order(), documented.Keys.Order());
        foreach (var (key, codes) in expected)
        {
            var text = documented[key]["responses"]!["409"]!["description"]!.GetValue<string>();
            Assert.All(codes, code => Assert.Contains(code, text));
        }
    }

    [Fact]
    public async Task Spec_DocumentedErrors_MatchWhatTheRealApiReturns()
    {
        var spec = await GenerateSpecAsync();
        var (client, _) = await api.CreateSignedInClientAsync();

        foreach (var (key, operation) in Operations(spec))
        {
            var (method, path) = (key.Split(' ')[0], RealPath(key.Split(' ')[1]));

            if (HasResponse(operation, "401"))
            {
                var anonymous = await Send(api.CreateAnonymousClient(), method, path);
                Assert.True(anonymous.StatusCode == HttpStatusCode.Unauthorized, $"{key} documents 401 but anonymous got {(int)anonymous.StatusCode}.");
            }

            if ((operation["description"]?.GetValue<string>() ?? string.Empty).Contains("policy AdminOnly", StringComparison.Ordinal))
            {
                var forbidden = await Send(client, method, path);
                Assert.True(forbidden.StatusCode == HttpStatusCode.Forbidden, $"{key} documents 403 for non-admins but a client got {(int)forbidden.StatusCode}.");
            }
        }
    }

    [Fact]
    public async Task Spec_SuccessCodes_MatchWhatTheCreateAndUpdateRoutesReturn()
    {
        var spec = await GenerateSpecAsync();

        // Each status is asserted against the real API by AdminCatalogueTests, AdminCreateStaffTests and AdminSelfLockoutTests.
        var expected = new Dictionary<string, string>
        {
            ["POST /api/v1/admin/services"] = "201",
            ["PUT /api/v1/admin/services/{id}"] = "204",
            ["PUT /api/v1/admin/services/{id}/active"] = "204",
            ["PUT /api/v1/admin/timeslots/{id}/block"] = "204",
            ["PUT /api/v1/admin/timeslots/{id}/unblock"] = "204",
            ["DELETE /api/v1/admin/timeslots/{id}"] = "204",
            ["POST /api/v1/admin/users"] = "201",
            ["PUT /api/v1/admin/users/{id}/active"] = "204",
        };

        var operations = Operations(spec).ToDictionary(o => o.Key, o => o.Value);
        foreach (var (key, status) in expected)
        {
            Assert.True(HasResponse(operations[key], status), $"{key} must document {status}.");
            Assert.False(HasResponse(operations[key], "200"), $"{key} must not document a 200.");
        }
    }

    private static bool HasResponse(JsonObject operation, string status) => operation["responses"]?[status] is not null;

    /// <summary>A real URL for a templated path: ids become 1, or an empty guid for the user routes.</summary>
    private static string RealPath(string template) =>
        System.Text.RegularExpressions.Regex.Replace(template, @"\{\w+\}", template.Contains("/users/", StringComparison.Ordinal) ? Guid.Empty.ToString() : "1");

    private static Task<HttpResponseMessage> Send(HttpClient client, string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT")
        {
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        }

        return client.SendAsync(request);
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
