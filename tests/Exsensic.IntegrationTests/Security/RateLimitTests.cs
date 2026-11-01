using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Common;
using Exsensic.IntegrationTests.Bookings;

namespace Exsensic.IntegrationTests.Security;

/// <summary>
/// Registration is limited to 3 per minute per visitor, so a script can't mass-create accounts. The limit is
/// per IP: another visitor is not affected. (The login limit is covered in AuthApiTests.)
/// </summary>
public sealed class RateLimitTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    [Fact]
    public async Task Register_FourthAttemptInMinute_Returns429RateLimitedWithRetryAfter()
    {
        var client = ClientWithIp("10.0.3.1");
        for (var i = 0; i < 3; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/register", NewRegistration());
        }

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", NewRegistration());

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ErrorCodes.RateLimited, body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Register_LimitReachedByOneIp_OtherIpStillAllowed()
    {
        var busy = ClientWithIp("10.0.3.2");
        for (var i = 0; i < 4; i++)
        {
            await busy.PostAsJsonAsync("/api/v1/auth/register", NewRegistration());
        }

        var response = await ClientWithIp("10.0.3.3").PostAsJsonAsync("/api/v1/auth/register", NewRegistration());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private HttpClient ClientWithIp(string ip)
    {
        var client = api.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add(ExsensicHeaders.ClientIp, ip);
        return client;
    }

    private static RegisterRequest NewRegistration() => new(
        "Test User",
        "Test Co",
        $"user-{Guid.NewGuid():N}@example.com",
        "0100000000",
        "Password123",
        "Password123");
}
