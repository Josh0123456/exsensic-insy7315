namespace Exsensic.Core.Exceptions;

/// <summary>
/// Thrown when input passes the shape checks on the DTO but fails a rule only Core can check, for
/// example a requirement answer that isn't one of the template's options. The API turns it into
/// 400 validation_failed with the same per-field errors as automatic model validation (docs/CONTRACTS.md §7),
/// so the Web shows the messages beside the right fields.
/// </summary>
public sealed class RequestValidationException : Exception
{
    /// <summary>
    /// Creates the exception from a set of field errors.
    /// </summary>
    /// <param name="errors">Error messages keyed by field name, as the Web form names the fields.</param>
    public RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more fields are invalid.")
    {
        Errors = errors;
    }

    /// <summary>Error messages keyed by field name.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
