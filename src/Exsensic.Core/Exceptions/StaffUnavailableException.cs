using Exsensic.Contracts.Common;

namespace Exsensic.Core.Exceptions;

/// <summary>
/// Thrown when an admin assigns a staff member who isn't qualified for the service or already has a
/// booking at that time. The API turns it into 409 staff_unavailable (docs/CONTRACTS.md §7).
/// </summary>
public sealed class StaffUnavailableException : BusinessRuleException
{
    /// <summary>
    /// Creates the exception with a message that is safe to show to the user.
    /// </summary>
    /// <param name="message">Why the staff member can't be assigned; defaults to a general explanation.</param>
    public StaffUnavailableException(string message = "That staff member can't deliver this service at this time. Please choose someone else.")
        : base(ErrorCodes.StaffUnavailable, message)
    {
    }
}
