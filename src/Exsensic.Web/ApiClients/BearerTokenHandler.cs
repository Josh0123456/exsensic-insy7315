using System.Net.Http.Headers;
using Exsensic.Contracts.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Exsensic.Web.ApiClients;

/// <summary>Attaches the current cookie session's access token without retaining user state in a pooled handler.</summary>
/// <param name="contextAccessor">Provides the current request, not the handler's dependency-injection scope.</param>
/// <param name="options">Limits authenticated requests to the configured API base address.</param>
public sealed class BearerTokenHandler(IHttpContextAccessor contextAccessor, ApiClientOptions options)
    : DelegatingHandler
{
    /// <summary>Sends a request using the current session token; anonymous requests remain anonymous.</summary>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.RequestUri is null || !options.BaseAddress.IsBaseOf(request.RequestUri))
        {
            throw new InvalidOperationException("API requests must stay within the configured Api:BaseUrl.");
        }

        // Never use DefaultRequestHeaders or store a token on this pooled handler.
        request.Headers.Authorization = null;
        var context = contextAccessor.HttpContext;

        // Pass the visitor's IP so the API's per-IP rate limits apply per visitor, not to everyone at once.
        request.Headers.Remove(ExsensicHeaders.ClientIp);
        if (context?.Connection.RemoteIpAddress is { } visitorIp)
        {
            request.Headers.Add(ExsensicHeaders.ClientIp, visitorIp.ToString());
        }
        if (context?.User.Identity?.IsAuthenticated == true)
        {
            var token = await context.GetTokenAsync(
                CookieAuthenticationDefaults.AuthenticationScheme, "access_token");
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await base.SendAsync(request, cancellationToken);
    }
}
