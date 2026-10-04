namespace Exsensic.Contracts.Common;

/// <summary>
/// The machine-readable error codes sent in the "code" extension of every ProblemDetails
/// response (docs/CONTRACTS.md §7). The API sets them and the Web reads them, so both sides
/// use these constants instead of typing the strings, and a typo becomes a compile error.
/// Each value is identical to its documented wire code and must never change.
/// </summary>
public static class ErrorCodes
{
    /// <summary>400: bad input. The body is ValidationProblemDetails with per-field errors.</summary>
    public const string ValidationFailed = "validation_failed";

    /// <summary>401: no token or an expired token. Login failures use one generic message.</summary>
    public const string Unauthenticated = "unauthenticated";

    /// <summary>403: signed in, but the wrong role for this endpoint.</summary>
    public const string Forbidden = "forbidden";

    /// <summary>404: missing, or a booking that belongs to someone else (never reveal it exists).</summary>
    public const string NotFound = "not_found";

    /// <summary>409: the time slot is taken, blocked or in the past.</summary>
    public const string SlotUnavailable = "slot_unavailable";

    /// <summary>409: the action is not allowed from the booking's current status.</summary>
    public const string InvalidTransition = "invalid_transition";

    /// <summary>409: the RowVersion is stale because someone else changed the record first.</summary>
    public const string ConcurrencyConflict = "concurrency_conflict";

    /// <summary>409: the staff member is not qualified for the service or is already booked then.</summary>
    public const string StaffUnavailable = "staff_unavailable";

    /// <summary>409: an admin tried to block or delete a slot that has an active booking.</summary>
    public const string SlotHasBooking = "slot_has_booking";

    /// <summary>409: the email address or service name already exists.</summary>
    public const string Duplicate = "duplicate";

    /// <summary>409: a client tried to cancel inside the cancellation cut-off window.</summary>
    public const string CancelWindowClosed = "cancel_window_closed";

    /// <summary>429: too many requests. The Retry-After header says when to try again.</summary>
    public const string RateLimited = "rate_limited";

    /// <summary>500: unexpected failure. Generic message only; the details are in the logs.</summary>
    public const string ServerError = "server_error";
}
