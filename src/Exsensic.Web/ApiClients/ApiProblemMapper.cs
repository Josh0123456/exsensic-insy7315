using System.Text.Json;
using System.Text.RegularExpressions;

namespace Exsensic.Web.ApiClients;

/// <summary>Maps untrusted ProblemDetails bodies to safe, consistent UI messages.</summary>
public sealed class ApiProblemMapper
{
    /// <summary>Parses a failure body, retaining validation and retry metadata without exposing diagnostic detail.</summary>
    public async Task<ApiProblem> MapAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        string? code = null;
        string? traceId = null;
        var fields = new Dictionary<string, string[]>(StringComparer.Ordinal);
        try
        {
            // Bound error bodies, including HTML from a proxy or an accidental developer exception page.
            if (response.Content.Headers.ContentLength > 64 * 1024)
            {
                throw new JsonException("Error response exceeds the parsing limit.");
            }
            await response.Content.LoadIntoBufferAsync(64 * 1024, cancellationToken);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                code = ReadIdentifier(root, "code");
                traceId = ReadIdentifier(root, "traceId");
                if (status == 400 && root.TryGetProperty("errors", out var errors)
                    && errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in errors.EnumerateObject().Take(100))
                    {
                        if (field.Name.Length > 200 || field.Value.ValueKind != JsonValueKind.Array) continue;
                        var messages = field.Value.EnumerateArray()
                            .Where(item => item.ValueKind == JsonValueKind.String)
                            .Take(10)
                            .Select(item => SafeValidationMessage(item.GetString()!))
                            .Where(message => !string.IsNullOrWhiteSpace(message))
                            .ToArray();
                        if (messages.Length > 0) fields[field.Name] = messages;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Missing/malformed JSON or a non-ProblemDetails body falls back to HTTP status.
        }
        catch (HttpRequestException)
        {
            // An oversized/unreadable error body must not replace the safe HTTP-status fallback.
        }
        catch (IOException)
        {
            // The response status is still useful even if reading the body fails.
        }

        var retry = status == 429 ? response.Headers.RetryAfter : null;
        return new ApiProblem
        {
            StatusCode = status,
            Code = code,
            Message = FriendlyMessage(status, code),
            TraceId = traceId,
            FieldErrors = fields,
            RetryAfterDelay = retry?.Delta,
            RetryAfterUtc = retry?.Date
        };
    }

    private static string? ReadIdentifier(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return text is not null && Regex.IsMatch(text, @"\A[A-Za-z0-9:._-]{1,128}\z") ? text : null;
    }

    private static string SafeValidationMessage(string message)
    {
        // Validation prose is encoded by Razor. Suppress diagnostic-shaped content even in this field.
        if (message.Length > 1000 || message.Any(char.IsControl)
            || message.Contains("Exception", StringComparison.OrdinalIgnoreCase)
            || message.Contains("StackTrace", StringComparison.OrdinalIgnoreCase))
        {
            return "Please check this value and try again.";
        }

        return message;
    }

    private static string FriendlyMessage(int status, string? code)
    {
        // ErrorCodes has not been merged into Contracts. These are the exact documented wire values,
        // not a duplicate constants class. Replace literals with shared constants once available.
        return (status, code) switch
        {
            (>= 500, _) => "Something went wrong with the service. Please try again later.",
            (401, _) => "We could not authenticate you. Please sign in or check your sign-in details.",
            (403, _) => "You do not have permission to do that.",
            (404, _) => "The requested item could not be found.",
            (429, _) => "Too many requests. Please wait before trying again.",
            (400, "validation_failed") => "Please check the highlighted fields and try again.",
            (409, "slot_unavailable") => "That time slot is no longer available. Please choose another.",
            (409, "invalid_transition") => "That action is no longer available for this booking. Please refresh the page.",
            (409, "concurrency_conflict") => "Someone else changed this record. Please refresh and review it before trying again.",
            (409, "staff_unavailable") => "That staff member is unavailable. Please choose another.",
            (409, "slot_has_booking") => "This time slot has an active booking and cannot be changed that way.",
            (409, "duplicate") => "An item with those details already exists. Please check your entries.",
            (409, "cancel_window_closed") => "The cancellation deadline has passed. Please contact the team for help.",
            (400, _) => "Please check your entries and try again.",
            (409, _) => "The record has changed or the action is unavailable. Please refresh and try again.",
            _ => "We could not complete your request. Please try again."
        };
    }
}
