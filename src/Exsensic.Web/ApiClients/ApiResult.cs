namespace Exsensic.Web.ApiClients;

/// <summary>Small Web transport result distinguishing JSON, empty success, and safe failure.</summary>
/// <typeparam name="T">The shared response DTO, once supplied by Contracts.</typeparam>
public sealed class ApiResult<T>
{
    internal ApiResult(int? statusCode, T? value, bool hasContent, ApiProblem? error)
    {
        StatusCode = statusCode;
        Value = value;
        HasContent = hasContent;
        Error = error;
    }

    /// <summary>Actual HTTP status, or null for a transport failure.</summary>
    public int? StatusCode { get; }

    /// <summary>Deserialized JSON on success; check HasContent before using it.</summary>
    public T? Value { get; }

    /// <summary>True when a JSON response value was returned.</summary>
    public bool HasContent { get; }

    /// <summary>True for successful HTTP responses, including no-content responses.</summary>
    public bool IsSuccess => Error is null;

    /// <summary>Friendly error metadata on failure.</summary>
    public ApiProblem? Error { get; }
}
