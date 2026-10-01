namespace Exsensic.Web.ApiClients;

/// <summary>Web-only presentation of an API or transport failure, never a replacement shared DTO.</summary>
public sealed class ApiProblem
{
    /// <summary>Actual HTTP status, or null when no HTTP response was received.</summary>
    public int? StatusCode { get; init; }

    /// <summary>API-supplied wire code, when present; transport failures do not invent shared codes.</summary>
    public string? Code { get; init; }

    /// <summary>Locally chosen user-friendly summary, suitable for encoded page or toast output.</summary>
    public required string Message { get; init; }

    /// <summary>Optional correlation identifier supplied by the API.</summary>
    public string? TraceId { get; init; }

    /// <summary>Field names and plain-text messages for ModelState integration.</summary>
    public IReadOnlyDictionary<string, string[]> FieldErrors { get; init; }
        = new Dictionary<string, string[]>();

    /// <summary>Server-requested delay, for the delta-seconds form of Retry-After.</summary>
    public TimeSpan? RetryAfterDelay { get; init; }

    /// <summary>Server-requested retry time, for the HTTP-date form of Retry-After.</summary>
    public DateTimeOffset? RetryAfterUtc { get; init; }
}
