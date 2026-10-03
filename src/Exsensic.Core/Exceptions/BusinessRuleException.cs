using Exsensic.Contracts.Common;

namespace Exsensic.Core.Exceptions;

/// <summary>
/// Thrown when a request is well-formed but breaks a business rule, for example cancelling inside the
/// cut-off window. The API turns every BusinessRuleException into 409 Conflict and sends its
/// <see cref="Code"/> in the ProblemDetails response (docs/CONTRACTS.md §7).
/// </summary>
/// <remarks>
/// The message is shown to the user, so it must be plain English and never contain ids or personal data.
/// The more specific booking exceptions inherit from this class, so the API needs only one mapping for all of them.
/// </remarks>
public class BusinessRuleException : Exception
{
    /// <summary>
    /// Creates the exception with the error code the API should send.
    /// </summary>
    /// <param name="code">One of the 409 codes in <see cref="ErrorCodes"/>, for example cancel_window_closed.</param>
    /// <param name="message">A plain-English explanation that is safe to show to the user.</param>
    public BusinessRuleException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>The ProblemDetails code the API sends for this error.</summary>
    public string Code { get; }
}
