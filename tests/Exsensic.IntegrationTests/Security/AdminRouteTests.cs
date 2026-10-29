using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Common;
using Exsensic.IntegrationTests.Bookings;

namespace Exsensic.IntegrationTests.Security;

/// <summary>
/// Role enforcement on every admin catalogue and user endpoint (FR26–FR29, FR32). Nobody signed in gets
/// 401, a client or staff member gets 403, and only an admin gets through. The API enforces this, not the
/// Web app hiding its buttons.
/// </summary>
public sealed class AdminRouteTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    private static readonly Guid SomeUserId = Guid.NewGuid();

    public static TheoryData<string, string> AdminEndpoints => new()
    {
        { "GET", "/api/v1/admin/services" },
        { "POST", "/api/v1/admin/services" },
        { "PUT", "/api/v1/admin/services/1" },
        { "PUT", "/api/v1/admin/services/1/active" },
        { "GET", "/api/v1/admin/timeslots" },
        { "POST", "/api/v1/admin/timeslots/generate" },
        { "PUT", "/api/v1/admin/timeslots/1/block" },
        { "PUT", "/api/v1/admin/timeslots/1/unblock" },
        { "DELETE", "/api/v1/admin/timeslots/1" },
        { "GET", "/api/v1/admin/users" },
        { "POST", "/api/v1/admin/users" },
        { "PUT", $"/api/v1/admin/users/{SomeUserId}/active" },
    };

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task AdminEndpoint_Anonymous_Returns401(string method, string path)
    {
        var response = await SendAsync(api.CreateAnonymousClient(), method, path);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, ErrorCodes.Unauthenticated);
    }

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task AdminEndpoint_Client_Returns403(string method, string path)
    {
        var (client, _) = await api.CreateSignedInClientAsync(RoleNames.Client);

        var response = await SendAsync(client, method, path);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, ErrorCodes.Forbidden);
    }

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task AdminEndpoint_Staff_Returns403(string method, string path)
    {
        var (staff, _) = await api.CreateSignedInClientAsync(RoleNames.Staff);

        var response = await SendAsync(staff, method, path);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, ErrorCodes.Forbidden);
    }

    [Theory]
    [InlineData("/api/v1/admin/services")]
    [InlineData("/api/v1/admin/timeslots")]
    [InlineData("/api/v1/admin/users")]
    public async Task AdminListEndpoint_Admin_Returns200(string path)
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);

        var response = await admin.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new { });
        }

        return client.SendAsync(request);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}
