using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Exsensic.Web.TagHelpers;

/// <summary>Adds server-validation state to normal MVC-bound form controls.</summary>
[HtmlTargetElement("input", Attributes = "asp-for")]
[HtmlTargetElement("textarea", Attributes = "asp-for")]
[HtmlTargetElement("select", Attributes = "asp-for")]
public sealed class FieldAccessibilityTagHelper : TagHelper
{
    /// <summary>The existing MVC expression; no alternate binding mechanism.</summary>
    [HtmlAttributeName("asp-for")]
    public ModelExpression For { get; set; } = null!;

    /// <summary>Current view and its ModelState.</summary>
    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    /// <summary>Runs after MVC's built-in form control helpers.</summary>
    public override int Order => 1000;

    /// <summary>Marks invalid controls without changing their validation or values.</summary>
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var key = ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(For.Name);
        var invalid = ViewContext.ViewData.ModelState.TryGetValue(key, out var state) && state.Errors.Count > 0;
        output.Attributes.SetAttribute("aria-invalid", invalid ? "true" : "false");
        if (For.Metadata.IsRequired && output.Attributes["type"]?.Value?.ToString() != "hidden")
        {
            output.Attributes.SetAttribute("required", "required");
            output.Attributes.SetAttribute("aria-required", "true");
        }
    }
}
