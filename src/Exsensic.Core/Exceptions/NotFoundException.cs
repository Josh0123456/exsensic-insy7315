namespace Exsensic.Core.Exceptions;

/// <summary>
/// Thrown when a record doesn't exist, or exists but the caller may not see it. The API turns it into
/// 404 not_found with a generic message, so it never reveals whether someone else's booking exists
/// (docs/CONTRACTS.md §7 and §12, decision 10).
/// </summary>
public sealed class NotFoundException : Exception
{
    /// <summary>
    /// Creates the exception. The message is for the logs only; the API always sends a generic message.
    /// </summary>
    /// <param name="resourceName">What was looked for, for example "Booking", used in the log message.</param>
    public NotFoundException(string resourceName)
        : base($"{resourceName} was not found.")
    {
        ResourceName = resourceName;
    }

    /// <summary>What was looked for, for example "Booking".</summary>
    public string ResourceName { get; }
}
