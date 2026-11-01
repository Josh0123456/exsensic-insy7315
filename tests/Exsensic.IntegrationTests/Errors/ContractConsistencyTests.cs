using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exsensic.Api.Errors;
using Exsensic.Contracts.Admin;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Staff;
using Exsensic.IntegrationTests.Bookings;

namespace Exsensic.IntegrationTests.Errors;

/// <summary>
/// Checks the API against docs/CONTRACTS.md section 6 (who may call each route) and section 7 (what each
/// failure looks like) route by route, so a new or changed endpoint that breaks the contract fails here.
/// Every error must be ProblemDetails with a "code" and a "traceId".
/// </summary>
public sealed class ContractConsistencyTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    private const string Client = "Client";
    private const string Staff = "Staff";
    private const string Admin = "Admin";

    /// <summary>Every protected route in the contract with the roles allowed to call it. A body is sent where one is expected.</summary>
    public static TheoryData<string, string, string> ProtectedRoutes() => new()
    {
        { "GET", "/api/v1/me", "Client,Staff,Admin" },
        { "PUT", "/api/v1/me", "Client,Staff,Admin" },
        { "POST", "/api/v1/bookings", "Client" },
        { "GET", "/api/v1/bookings/mine", "Client" },
        { "PUT", "/api/v1/bookings/1/reschedule", "Client,Admin" },
        { "PUT", "/api/v1/bookings/1/cancel", "Client,Admin" },
        { "GET", "/api/v1/admin/dashboard", "Admin" },
        { "GET", "/api/v1/admin/bookings", "Admin" },
        { "GET", "/api/v1/admin/staff?serviceId=1&timeSlotId=1", "Admin" },
        { "PUT", "/api/v1/admin/bookings/1/confirm", "Admin" },
        { "PUT", "/api/v1/admin/bookings/1/reject", "Admin" },
        { "GET", "/api/v1/staff/me/bookings", "Staff" },
        { "PUT", "/api/v1/staff/bookings/1/complete", "Staff,Admin" },
        { "GET", "/api/v1/notifications/mine", "Client,Staff,Admin" },
        { "PUT", "/api/v1/notifications/1/read", "Client,Staff,Admin" },
        { "GET", "/api/v1/admin/services", "Admin" },
        { "POST", "/api/v1/admin/services", "Admin" },
        { "PUT", "/api/v1/admin/services/1", "Admin" },
        { "PUT", "/api/v1/admin/services/1/active", "Admin" },
        { "GET", "/api/v1/admin/timeslots", "Admin" },
        { "POST", "/api/v1/admin/timeslots/generate", "Admin" },
        { "PUT", "/api/v1/admin/timeslots/1/block", "Admin" },
        { "PUT", "/api/v1/admin/timeslots/1/unblock", "Admin" },
        { "DELETE", "/api/v1/admin/timeslots/1", "Admin" },
        { "GET", "/api/v1/admin/users", "Admin" },
        { "POST", "/api/v1/admin/users", "Admin" },
        { "PUT", $"/api/v1/admin/users/{Guid.Empty}/active", "Admin" },
    };

    /// <summary>No token on a protected route: 401 unauthenticated, as ProblemDetails, before anything else is checked.</summary>
    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task ProtectedRoute_NoToken_Returns401Unauthenticated(string method, string path, string allowedRoles)
    {
        _ = allowedRoles;

        var response = await SendAsync(api.CreateAnonymousClient(), method, path);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "unauthenticated");
    }

    /// <summary>A signed-in user of a role the contract doesn't list: 403 forbidden, before the body or the id is looked at.</summary>
    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task ProtectedRoute_WrongRole_Returns403Forbidden(string method, string path, string allowedRoles)
    {
        var allowed = allowedRoles.Split(',');
        foreach (var role in new[] { Client, Staff, Admin }.Except(allowed))
        {
            var (user, _) = await api.CreateSignedInClientAsync(role);

            var response = await SendAsync(user, method, path);

            await AssertProblemAsync(response, HttpStatusCode.Forbidden, "forbidden");
        }
    }

    /// <summary>The right role on a made-up booking id: 404 not_found, never 403 or 500.</summary>
    [Theory]
    [InlineData("GET", "/api/v1/bookings/999999", Client)]
    [InlineData("PUT", "/api/v1/bookings/999999/reschedule", Client)]
    [InlineData("PUT", "/api/v1/bookings/999999/cancel", Client)]
    [InlineData("GET", "/api/v1/bookings/999999", Admin)]
    [InlineData("PUT", "/api/v1/admin/bookings/999999/confirm", Admin)]
    [InlineData("PUT", "/api/v1/admin/bookings/999999/reject", Admin)]
    [InlineData("PUT", "/api/v1/staff/bookings/999999/complete", Staff)]
    [InlineData("PUT", "/api/v1/staff/bookings/999999/complete", Admin)]
    [InlineData("PUT", "/api/v1/notifications/999999/read", Client)]
    public async Task BookingRoute_UnknownId_Returns404NotFound(string method, string path, string role)
    {
        var (user, _) = await api.CreateSignedInClientAsync(role);

        var response = await SendAsync(user, method, path, ValidBody(path));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "not_found");
    }

    /// <summary>A malformed JSON body on a route that takes one: 400 validation_failed, never 500.</summary>
    [Theory]
    [InlineData("POST", "/api/v1/bookings", Client)]
    [InlineData("PUT", "/api/v1/bookings/1/reschedule", Client)]
    [InlineData("PUT", "/api/v1/bookings/1/cancel", Client)]
    [InlineData("PUT", "/api/v1/admin/bookings/1/confirm", Admin)]
    [InlineData("PUT", "/api/v1/admin/bookings/1/reject", Admin)]
    [InlineData("PUT", "/api/v1/staff/bookings/1/complete", Staff)]
    public async Task BodyRoute_MalformedJson_Returns400ValidationFailed(string method, string path, string role)
    {
        var (user, _) = await api.CreateSignedInClientAsync(role);
        var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json") };

        var response = await user.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
    }

    /// <summary>A body with the required fields missing: 400 validation_failed with a per-field error list.</summary>
    [Theory]
    [InlineData("PUT", "/api/v1/bookings/1/reschedule", Client)]
    [InlineData("PUT", "/api/v1/admin/bookings/1/confirm", Admin)]
    [InlineData("PUT", "/api/v1/admin/bookings/1/reject", Admin)]
    [InlineData("PUT", "/api/v1/staff/bookings/1/complete", Staff)]
    public async Task BodyRoute_EmptyObject_Returns400WithFieldErrors(string method, string path, string role)
    {
        var (user, _) = await api.CreateSignedInClientAsync(role);

        var response = await SendAsync(user, method, path, "{}");

        var body = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        Assert.True(body.TryGetProperty("errors", out var errors) && errors.EnumerateObject().Any(), "A validation error lists the fields at fault.");
    }

    /// <summary>A wrong HTTP method is the caller's mistake: 405 with a client-error code, never "server_error".</summary>
    [Theory]
    [InlineData("DELETE", "/api/v1/bookings/mine")]
    [InlineData("POST", "/api/v1/bookings/1")]
    [InlineData("PUT", "/api/v1/bookings")]
    public async Task SignedInRoute_WrongHttpMethod_Returns405WithClientErrorCode(string method, string path)
    {
        var (client, _) = await api.CreateSignedInClientAsync();

        var response = await SendAsync(client, method, path);

        await AssertProblemAsync(response, HttpStatusCode.MethodNotAllowed, "validation_failed");
    }

    /// <summary>A body that isn't JSON, or no body at all, is the caller's mistake: 415 with a client-error code.</summary>
    [Theory]
    [InlineData("text/plain", "hello")]
    [InlineData(null, null)]
    public async Task BodyRoute_NotJson_Returns415WithClientErrorCode(string? contentType, string? content)
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/bookings");
        if (contentType is not null)
        {
            request.Content = new StringContent(content!, System.Text.Encoding.UTF8, contentType);
        }

        var response = await client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "validation_failed");
    }

    /// <summary>"server_error" is reserved for 5xx; every other status the framework writes has its own client-error code.</summary>
    [Theory]
    [InlineData(400, "validation_failed")]
    [InlineData(401, "unauthenticated")]
    [InlineData(403, "forbidden")]
    [InlineData(404, "not_found")]
    [InlineData(405, "validation_failed")]
    [InlineData(406, "validation_failed")]
    [InlineData(413, "validation_failed")]
    [InlineData(415, "validation_failed")]
    [InlineData(429, "rate_limited")]
    [InlineData(500, "server_error")]
    [InlineData(503, "server_error")]
    public void CodeForStatus_AnyStatus_NeverCallsAClientErrorServerError(int status, string expectedCode)
    {
        Assert.Equal(expectedCode, ErrorHandlingExtensions.CodeForStatus(status));
    }

    /// <summary>The 201 for a new booking carries a Location that the owner can follow to the booking.</summary>
    [Fact]
    public async Task PostBookings_Created_LocationLeadsToTheBooking()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var serviceId = await api.AddServiceAsync();
        var slotId = await api.AddFutureSlotAsync();

        var created = await client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(serviceId, slotId, new Dictionary<string, string>
        {
            ["shootType"] = "Product",
            ["location"] = "Studio",
            ["quantity"] = "3",
            ["description"] = "Location header check.",
        }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var followed = await client.GetAsync(created.Headers.Location);

        Assert.Equal(HttpStatusCode.OK, followed.StatusCode);
        Assert.Equal((await created.Content.ReadFromJsonAsync<BookingCreatedDto>())!.Id, (await followed.Content.ReadFromJsonAsync<BookingDetailDto>())!.Id);
    }

    /// <summary>The booking-create limit is 10 per 10 minutes per user: the 11th request is 429 rate_limited with Retry-After.</summary>
    [Fact]
    public async Task PostBookings_EleventhRequestInWindow_Returns429RateLimitedWithRetryAfter()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var request = new CreateBookingRequest(0, 0, new Dictionary<string, string>());

        for (var i = 0; i < 10; i++)
        {
            var allowed = await client.PostAsJsonAsync("/api/v1/bookings", request);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        var limited = await client.PostAsJsonAsync("/api/v1/bookings", request);

        await AssertProblemAsync(limited, HttpStatusCode.TooManyRequests, "rate_limited");
        Assert.True(limited.Headers.TryGetValues("Retry-After", out var retryAfter) && int.Parse(retryAfter.Single()) > 0);
    }

    /// <summary>A valid-looking body for the route, so a missing booking is the only thing wrong.</summary>
    private static string ValidBody(string path)
    {
        var version = Convert.ToBase64String(new byte[8]);
        return path switch
        {
            _ when path.EndsWith("/reschedule", StringComparison.Ordinal) => JsonSerializer.Serialize(new RescheduleBookingRequest(1, version)),
            _ when path.EndsWith("/cancel", StringComparison.Ordinal) => JsonSerializer.Serialize(new CancelBookingRequest(null, version)),
            _ when path.EndsWith("/confirm", StringComparison.Ordinal) => JsonSerializer.Serialize(new ConfirmBookingRequest(Guid.NewGuid(), version)),
            _ when path.EndsWith("/reject", StringComparison.Ordinal) => JsonSerializer.Serialize(new RejectBookingRequest("No capacity.", version)),
            _ when path.EndsWith("/complete", StringComparison.Ordinal) => JsonSerializer.Serialize(new CompleteBookingRequest(version)),
            _ => "{}",
        };
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path, string body = "{}")
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT")
        {
            request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        }

        return client.SendAsync(request);
    }

    /// <summary>Checks the status, the ProblemDetails content type, and the "code" and "traceId" extensions.</summary>
    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Expected {(int)status} but got {(int)response.StatusCode}: {text}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = JsonDocument.Parse(text).RootElement.Clone();
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        return body;
    }
}
