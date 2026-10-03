using System.Globalization;
using Exsensic.Contracts.Enums;
using Exsensic.Contracts.Requirements;

namespace Exsensic.Core.Requirements;

/// <summary>
/// Checks a client's requirement answers against their service category's template before a booking
/// is created. The API never trusts the form: anything not in the template is rejected, so a crafted
/// request can't store extra keys or oversized text.
/// </summary>
public static class RequirementValidator
{
    /// <summary>
    /// Validates the answers and returns every problem found, so the client can fix them all at once.
    /// </summary>
    /// <param name="category">The category of the service being booked; it decides the template.</param>
    /// <param name="answers">The submitted answers, keyed by field key. Null is treated as no answers.</param>
    /// <returns>
    /// Error messages keyed by field key, ready for a 400 validation_failed response.
    /// An empty dictionary means the answers are valid.
    /// </returns>
    public static IReadOnlyDictionary<string, string[]> Validate(
        ServiceCategory category,
        IReadOnlyDictionary<string, string>? answers)
    {
        var template = RequirementTemplates.For(category);
        var fields = template.Fields.ToDictionary(f => f.Key, StringComparer.Ordinal);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        answers ??= new Dictionary<string, string>();

        // Unknown keys are rejected rather than ignored, so nothing outside the template is ever stored.
        foreach (var key in answers.Keys.Where(k => !fields.ContainsKey(k)))
        {
            errors[key] = ["This field is not part of the requirement form for this service."];
        }

        foreach (var field in template.Fields)
        {
            answers.TryGetValue(field.Key, out var value);
            var error = CheckField(field, value);
            if (error is not null)
            {
                errors[field.Key] = [error];
            }
        }

        return errors;
    }

    /// <summary>
    /// Checks one answer against its field definition and returns the first problem, or null if it is valid.
    /// A blank optional answer is valid and skips the other checks.
    /// </summary>
    private static string? CheckField(RequirementFieldDto field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return field.Required ? $"{field.Label} is required." : null;
        }

        if (field.MaxLength is int maxLength && value.Length > maxLength)
        {
            return $"{field.Label} must be {maxLength} characters or fewer.";
        }

        return field.InputType switch
        {
            RequirementInputTypes.Select => CheckSelect(field, value),
            RequirementInputTypes.Number => CheckNumber(field, value),
            RequirementInputTypes.Url => CheckUrl(field, value),
            _ => null,
        };
    }

    /// <summary>The answer must exactly match one of the listed options.</summary>
    private static string? CheckSelect(RequirementFieldDto field, string value) =>
        field.Options.Contains(value, StringComparer.Ordinal)
            ? null
            : $"{field.Label} must be one of: {string.Join(", ", field.Options)}.";

    /// <summary>
    /// The answer must be a whole number inside the field's range. Parsed with the invariant culture so
    /// the result doesn't depend on the server's regional settings.
    /// </summary>
    private static string? CheckNumber(RequirementFieldDto field, string value)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return $"{field.Label} must be a whole number.";
        }

        if ((field.Min is decimal min && number < min) || (field.Max is decimal max && number > max))
        {
            return $"{field.Label} must be between {field.Min} and {field.Max}.";
        }

        return null;
    }

    /// <summary>
    /// The answer must be an absolute http or https address. Other schemes (for example javascript:)
    /// are rejected so a stored link can never run script when staff open it.
    /// </summary>
    private static string? CheckUrl(RequirementFieldDto field, string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? null
            : $"{field.Label} must be a web address starting with http:// or https://.";
}
