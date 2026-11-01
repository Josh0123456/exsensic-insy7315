using System.Net;
using Exsensic.IntegrationTests.Bookings;

namespace Exsensic.IntegrationTests.Security;

/// <summary>
/// Security misconfiguration (OWASP A02). Every API response, success or error, carries the hardening
/// headers, and none of them can be cached.
/// </summary>
public sealed class SecurityHeaderTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    [Theory]
    [InlineData("/health", HttpStatusCode.OK)]
    [InlineData("/api/v1/services", HttpStatusCode.OK)]
    [InlineData("/api/v1/services/999999", HttpStatusCode.NotFound)]
    [InlineData("/api/v1/me", HttpStatusCode.Unauthorized)]
    public async Task ApiResponse_AnyStatus_HasSecurityHeaders(string path, HttpStatusCode expectedStatus)
    {
        var response = await api.CreateAnonymousClient().GetAsync(path);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'", Header(response, "Content-Security-Policy"));
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
        Assert.Equal("camera=(), microphone=(), geolocation=()", Header(response, "Permissions-Policy"));
        Assert.Equal("no-store", Header(response, "Cache-Control"));
    }

    [Fact]
    public async Task ApiResponse_DoesNotAdvertiseTheServer()
    {
        var response = await api.CreateAnonymousClient().GetAsync("/health");

        Assert.False(response.Headers.Contains("X-Powered-By"));
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values)
            ? string.Join(", ", values)
            : string.Empty;
}
