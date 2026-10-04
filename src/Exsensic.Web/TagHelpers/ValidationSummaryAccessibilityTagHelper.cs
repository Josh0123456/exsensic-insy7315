using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Exsensic.Web.TagHelpers;

/// <summary>Links server errors to their fields without rendering untrusted HTML.</summary>
[HtmlTargetElement("div", Attributes = "asp-validation-summary")]
public sealed class ValidationSummaryAccessibilityTagHelper : TagHelper
{
    /// <summary>The current validation state.</summary>
    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    /// <inheritdoc />
    public override int Order => 1100;

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.Attributes.SetAttribute("tabindex", "-1");
        output.Attributes.SetAttribute("data-validation-summary", "");
        output.Attributes.SetAttribute("aria-label", "Please check the following");
        if (ViewContext.ViewData.ModelState.IsValid) return;
        var list = new TagBuilder("ul");
        foreach (var entry in ViewContext.ViewData.ModelState)
        {
            foreach (var error in entry.Value!.Errors)
            {
                var item = new TagBuilder("li");
                var message = string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Please check this value." : error.ErrorMessage;
                if (string.IsNullOrEmpty(entry.Key) || entry.Key.EndsWith("RowVersion", StringComparison.Ordinal))
                    item.InnerHtml.Append(message);
                else
                {
                    var link = new TagBuilder("a");
                    link.Attributes["href"] = "#" + TagBuilder.CreateSanitizedId(entry.Key, "_");
                    link.Attributes["data-error-field"] = entry.Key;
                    link.InnerHtml.Append(message);
                    item.InnerHtml.AppendHtml(link);
                }
                list.InnerHtml.AppendHtml(item);
            }
        }
        // Replace the framework summary rather than retaining its appended list.
        output.PreContent.Clear();
        output.PostContent.Clear();
        output.Content.SetHtmlContent(list);
    }
}
