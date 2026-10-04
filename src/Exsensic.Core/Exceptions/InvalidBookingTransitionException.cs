using Exsensic.Contracts.Common;
using Exsensic.Contracts.Enums;
using Exsensic.Core.Bookings;

namespace Exsensic.Core.Exceptions;

/// <summary>
/// Thrown when an action is not allowed from the booking's current status, for example confirming
/// a booking that is already Completed. The API turns it into 409 with the code invalid_transition
/// (docs/CONTRACTS.md §3 and §7).
/// </summary>
public sealed class InvalidBookingTransitionException : BusinessRuleException
{
    /// <summary>
    /// Creates the exception for an action that the current status does not allow.
    /// </summary>
    /// <param name="currentStatus">The status the booking was in when the action was attempted.</param>
    /// <param name="action">The action that was attempted.</param>
    /// <param name="detail">An optional extra explanation, for example why Complete was refused.</param>
    public InvalidBookingTransitionException(BookingStatus currentStatus, BookingAction action, string? detail = null)
        : base(ErrorCodes.InvalidTransition, BuildMessage(currentStatus, action, detail))
    {
        CurrentStatus = currentStatus;
        Action = action;
    }

    /// <summary>The status the booking was in when the action was attempted.</summary>
    public BookingStatus CurrentStatus { get; }

    /// <summary>The action that was attempted.</summary>
    public BookingAction Action { get; }

    /// <summary>
    /// Builds a plain-English message that is safe to show to the user: it names the status and
    /// the action only, never ids or personal data.
    /// </summary>
    private static string BuildMessage(BookingStatus currentStatus, BookingAction action, string? detail)
    {
        var message = $"A {currentStatus} booking can't be changed with the action {action}.";
        return detail is null ? message : $"{message} {detail}";
    }
}
