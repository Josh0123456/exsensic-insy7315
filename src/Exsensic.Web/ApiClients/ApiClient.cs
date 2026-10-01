using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Exsensic.Web.ApiClients;

/// <summary>Shared JSON transport for later typed clients; contains no feature routes or business rules.</summary>
/// <param name="httpClient">Centrally configured API client.</param>
/// <param name="mapper">Maps HTTP failures into UI errors.</param>
/// <param name="logger">Records status-only diagnostics, never URLs, headers, bodies or exception details.</param>
public sealed class ApiClient(HttpClient httpClient, ApiProblemMapper mapper, ILogger<ApiClient> logger)
{
    /// <summary>Sends a bodyless request and reads a shared JSON response DTO.</summary>
    public Task<ApiResult<TResponse>> SendAsync<TResponse>(
        HttpMethod method, string relativePath, CancellationToken cancellationToken)
    {
        return SendCoreAsync<TResponse>(method, relativePath, null, false, cancellationToken);
    }

    /// <summary>Sends a shared request DTO as JSON and reads a shared response DTO.</summary>
    public Task<ApiResult<TResponse>> SendJsonAsync<TRequest, TResponse>(
        HttpMethod method, string relativePath, TRequest body, CancellationToken cancellationToken)
    {
        return SendCoreAsync<TResponse>(
            method, relativePath, JsonContent.Create(body), false, cancellationToken);
    }

    /// <summary>Sends a bodyless request whose successful response body is not needed.</summary>
    public Task<ApiResult<object>> SendNoContentAsync(
        HttpMethod method, string relativePath, CancellationToken cancellationToken)
    {
        return SendCoreAsync<object>(method, relativePath, null, true, cancellationToken);
    }

    /// <summary>Sends JSON for an operation whose successful response body is not needed.</summary>
    public Task<ApiResult<object>> SendJsonNoContentAsync<TRequest>(
        HttpMethod method, string relativePath, TRequest body, CancellationToken cancellationToken)
    {
        return SendCoreAsync<object>(
            method, relativePath, JsonContent.Create(body), true, cancellationToken);
    }

    private async Task<ApiResult<T>> SendCoreAsync<T>(
        HttpMethod method, string relativePath, HttpContent? content, bool discardSuccessBody,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage { Method = method, Content = content };
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.StartsWith('/')
            || relativePath.Contains('\\') || !Uri.TryCreate(relativePath, UriKind.Relative, out var relativeUri))
        {
            throw new ArgumentException("Use a relative API path without a leading slash.", nameof(relativePath));
        }

        request.RequestUri = relativeUri;
        int? status = null;
        try
        {
            // Default buffering keeps HttpClient's timeout active while the response body is downloaded.
            using var response = await httpClient.SendAsync(request, cancellationToken);
            status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                var error = await mapper.MapAsync(response, cancellationToken);
                logger.LogWarning("API request failed with HTTP status {StatusCode}.", status);
                return new ApiResult<T>(status, default, false, error);
            }

            if (discardSuccessBody || response.StatusCode == HttpStatusCode.NoContent
                || response.StatusCode == HttpStatusCode.ResetContent || method == HttpMethod.Head
                || response.Content.Headers.ContentLength == 0)
            {
                return new ApiResult<T>(status, default, false, null);
            }

            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            return new ApiResult<T>(status, value, value is not null, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure<T>(status, "The service took too long to respond. Please try again.");
        }
        catch (HttpRequestException)
        {
            return Failure<T>(status, "We could not reach the service. Please try again shortly.");
        }
        catch (IOException)
        {
            return Failure<T>(status, "The connection to the service was interrupted. Please try again.");
        }
        catch (JsonException)
        {
            return Failure<T>(status, "The service returned an unexpected response. Please try again later.");
        }
    }

    private ApiResult<T> Failure<T>(int? status, string message)
    {
        logger.LogWarning("API transport or response failure; HTTP status {StatusCode}.", status);
        return new ApiResult<T>(status, default, false, new ApiProblem { StatusCode = status, Message = message });
    }
}
