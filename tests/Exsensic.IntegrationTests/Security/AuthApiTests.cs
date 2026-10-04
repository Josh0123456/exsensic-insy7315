using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Common;
using Exsensic.IntegrationTests.Bookings;

namespace Exsensic.IntegrationTests.Security;

/// <summary>
/// Registration and sign-in through the real API. Each test uses its own client IP so the
/// per-IP rate limits don't leak between tests. The rate-limit test deliberately reuses one.
/// </summary>
public sealed class AuthApiTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
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

    /// <summary>A valid registration returns 201 with a signed-in token for the Client role.</summary>
    [Fact]
    public async Task Register_ValidRequest_Returns201WithClientToken()
    {
        var client = ClientWithIp("10.0.1.1");
        var request = NewRegistration();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrEmpty(auth.AccessToken));
        Assert.Equal(RoleNames.Client, auth.Role);
    }

    /// <summary>A second registration with the same email is 409 duplicate.</summary>
    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = ClientWithIp("10.0.1.2");
        var request = NewRegistration();
        await client.PostAsJsonAsync("/api/v1/auth/register", request);

        var second = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        await AssertProblemAsync(second, HttpStatusCode.Conflict, ErrorCodes.Duplicate);
    }

    /// <summary>A weak password returns a per-field error, so the Web form can show it.</summary>
    [Fact]
    public async Task Register_WeakPassword_Returns400WithPasswordError()
    {
        var client = ClientWithIp("10.0.1.3");
        var request = NewRegistration() with { Password = "short", ConfirmPassword = "short" };

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validation_failed", body.RootElement.GetProperty("code").GetString());
    }

    /// <summary>A wrong password uses the same generic 401 as an unknown email.</summary>
    [Fact]
    public async Task Login_WrongPassword_Returns401GenericMessage()
    {
        var register = ClientWithIp("10.0.1.4");
        var request = NewRegistration();
        await register.PostAsJsonAsync("/api/v1/auth/register", request);

        var login = ClientWithIp("10.0.1.5");
        var response = await login.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(request.Email, "WrongPassword123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Email or password is incorrect", await response.Content.ReadAsStringAsync());
    }

    /// <summary>Six login attempts in a minute from one IP are rate limited.</summary>
    [Fact]
    public async Task Login_SixthAttemptInMinute_Returns429WithRetryAfter()
    {
        var client = ClientWithIp("10.0.1.6");
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest("nobody@example.com", "WrongPassword123"));
        }

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("nobody@example.com", "WrongPassword123"));

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}
