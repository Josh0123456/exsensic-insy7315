namespace Exsensic.Contracts.Requirements;

/// <summary>
/// One question on a service category's requirement form (docs/CONTRACTS.md §5).
/// The Web renders an input from it, and the API validates submitted answers against it.
/// </summary>
/// <param name="Key">The key the answer is stored under, for example "shootType".</param>
/// <param name="Label">The label shown to the client, for example "Type of shoot".</param>
/// <param name="InputType">Which input to render: "text", "textarea", "number", "select" or "url".</param>
/// <param name="Required">Whether the client must answer this question.</param>
/// <param name="MaxLength">The longest allowed answer for text inputs, or null when not limited.</param>
/// <param name="Min">The smallest allowed value for number inputs, or null.</param>
/// <param name="Max">The largest allowed value for number inputs, or null.</param>
/// <param name="Options">The allowed values for select inputs; empty for every other input type.</param>
public sealed record RequirementFieldDto(
    string Key,
    string Label,
    string InputType,
    bool Required,
    int? MaxLength,
    decimal? Min,
    decimal? Max,
    IReadOnlyList<string> Options);
