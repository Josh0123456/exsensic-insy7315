using Exsensic.Contracts.Enums;
using Exsensic.Contracts.Requirements;

namespace Exsensic.Core.Requirements;

/// <summary>
/// The requirement form for each service category, defined in code so the questions are versioned with
/// the rules that check them. The Web renders the form from these templates and
/// <see cref="RequirementValidator"/> checks submitted answers against the same templates, so the form
/// and the validation can never disagree.
/// </summary>
/// <remarks>
/// Every category asks for a required "description" (the main brief) and an optional "notes" field,
/// so staff always find the brief under the same key.
/// </remarks>
public static class RequirementTemplates
{
    private const int DescriptionMaxLength = 1000;
    private const int NotesMaxLength = 500;
    private const int UrlMaxLength = 300;

    private static readonly IReadOnlyDictionary<ServiceCategory, RequirementTemplateDto> Templates =
        new Dictionary<ServiceCategory, RequirementTemplateDto>
        {
            [ServiceCategory.Photoshoot] = new(ServiceCategory.Photoshoot,
            [
                Select("shootType", "Type of shoot", required: true, "Product", "Team", "Brand/lifestyle"),
                Select("location", "Location", required: true, "Studio", "On location"),
                Number("quantity", "Number of products or people", required: true, min: 1, max: 500),
                Description("Describe what you want photographed and how the photos will be used"),
                Notes(),
            ]),
            [ServiceCategory.Website] = new(ServiceCategory.Website,
            [
                Select("projectType", "What do you need?", required: true, "New website", "Redesign", "Demonstration"),
                Url("currentWebsite", "Current website address (if you have one)", required: false),
                Number("pageCount", "Roughly how many pages", required: false, min: 1, max: 100),
                Description("Describe your business and what the website should achieve"),
                Notes(),
            ]),
            [ServiceCategory.Instagram] = new(ServiceCategory.Instagram,
            [
                Text("accountHandle", "Instagram handle", required: true, maxLength: 30),
                Select("focus", "Main focus", required: true, "Growth", "Content strategy", "Advertising", "Profile review"),
                Description("Describe your audience and what you want from the account"),
                Notes(),
            ]),
            [ServiceCategory.TikTok] = new(ServiceCategory.TikTok,
            [
                Text("accountHandle", "TikTok handle", required: true, maxLength: 24),
                Select("focus", "Main focus", required: true, "Content ideas", "Growth", "Advertising", "Account setup"),
                Description("Describe your audience and the kind of content you want to make"),
                Notes(),
            ]),
        };

    /// <summary>
    /// Returns the requirement template for a service category.
    /// </summary>
    /// <param name="category">The service's category.</param>
    /// <returns>The template with its fields in display order.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The category has no template (not one of the four contract values).</exception>
    public static RequirementTemplateDto For(ServiceCategory category) =>
        Templates.TryGetValue(category, out var template)
            ? template
            : throw new ArgumentOutOfRangeException(nameof(category), category, "No requirement template for this category.");

    private static RequirementFieldDto Select(string key, string label, bool required, params string[] options) =>
        new(key, label, RequirementInputTypes.Select, required, MaxLength: null, Min: null, Max: null, options);

    private static RequirementFieldDto Number(string key, string label, bool required, int min, int max) =>
        new(key, label, RequirementInputTypes.Number, required, MaxLength: null, min, max, []);

    private static RequirementFieldDto Text(string key, string label, bool required, int maxLength) =>
        new(key, label, RequirementInputTypes.Text, required, maxLength, Min: null, Max: null, []);

    private static RequirementFieldDto Url(string key, string label, bool required) =>
        new(key, label, RequirementInputTypes.Url, required, UrlMaxLength, Min: null, Max: null, []);

    private static RequirementFieldDto Description(string label) =>
        new("description", label, RequirementInputTypes.TextArea, Required: true, DescriptionMaxLength, Min: null, Max: null, []);

    private static RequirementFieldDto Notes() =>
        new("notes", "Anything else we should know", RequirementInputTypes.TextArea, Required: false, NotesMaxLength, Min: null, Max: null, []);
}
