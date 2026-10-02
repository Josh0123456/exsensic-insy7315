using Exsensic.Contracts.Common;

namespace Exsensic.Core.Exceptions;

/// <summary>
/// Thrown when the chosen time slot can't be booked: it is already taken, blocked, in the past or too
/// short for the service. The API turns it into 409 slot_unavailable (docs/CONTRACTS.md §7).
/// </summary>
public sealed class SlotUnavailableException : BusinessRuleException
{
    /// <summary>
    /// Creates the exception with a message that is safe to show to the user.
    /// </summary>
    /// <param name="message">Why the slot can't be used; defaults to a general explanation.</param>
    public SlotUnavailableException(string message = "That time slot is no longer available. Please choose another time.")
        : base(ErrorCodes.SlotUnavailable, message)
    {
    }
}
