namespace Exsensic.Core.Requirements;

/// <summary>
/// The input types a requirement field can have. The values are the strings documented on
/// RequirementFieldDto.InputType, so the Web knows which control to render for each field.
/// </summary>
public static class RequirementInputTypes
{
    /// <summary>A single-line text box.</summary>
    public const string Text = "text";

    /// <summary>A multi-line text box for longer answers.</summary>
    public const string TextArea = "textarea";

    /// <summary>A whole number between the field's Min and Max.</summary>
    public const string Number = "number";

    /// <summary>A drop-down list; the answer must be one of the field's Options.</summary>
    public const string Select = "select";

    /// <summary>An absolute http or https web address.</summary>
    public const string Url = "url";
}
