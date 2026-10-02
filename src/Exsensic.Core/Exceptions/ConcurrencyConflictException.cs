using Exsensic.Contracts.Common;

namespace Exsensic.Core.Exceptions;

/// <summary>
/// Thrown when the RowVersion sent with a change no longer matches the database, because someone else
/// changed the record first. The API turns it into 409 concurrency_conflict, and the Web offers a reload
/// (docs/CONTRACTS.md §7).
/// </summary>
public sealed class ConcurrencyConflictException : BusinessRuleException
{
    /// <summary>
    /// Creates the exception with a message that is safe to show to the user.
    /// </summary>
    /// <param name="message">What went wrong; defaults to a general explanation.</param>
    public ConcurrencyConflictException(string message = "This record was changed by someone else. Please reload it and try again.")
        : base(ErrorCodes.ConcurrencyConflict, message)
    {
    }
}
