using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Exsensic.Web.Models.Book;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Exsensic.Web.Journeys;

/// <summary>Validates UI shape against reloaded template metadata, without deciding booking policy.</summary>
public static class RequirementForm
{
    /// <summary>Checks the submitted controls against the real template and rejects unsupported control types.</summary>
    public static void Validate(RequirementsStepViewModel form, ModelStateDictionary modelState)
    {
        foreach (var field in form.Fields)
        {
            var key = $"Requirements[{field.Key}]";
            form.Requirements.TryGetValue(field.Key, out var value);
            if (!IsSupported(field.InputType))
            {
                modelState.AddModelError(string.Empty, "This requirement form needs an update before it can be submitted.");
                continue;
            }
            if (field.Required && (string.IsNullOrWhiteSpace(value) || (field.InputType == "checkbox" && value != "true")))
                modelState.AddModelError(key, $"{field.Label} is required.");
            if (string.IsNullOrEmpty(value)) continue;
            if (field.MaxLength is int maximum && value.Length > maximum)
                modelState.AddModelError(key, $"{field.Label} must be {maximum} characters or fewer.");
            if (field.InputType == "select" && !field.Options.Contains(value, StringComparer.Ordinal))
                modelState.AddModelError(key, "Choose one of the listed options.");
            if (field.InputType == "email" && !new EmailAddressAttribute().IsValid(value))
                modelState.AddModelError(key, "Enter a valid email address.");
            if (field.InputType == "date" && !DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                modelState.AddModelError(key, "Enter a valid date.");
            if (field.InputType == "number")
            {
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                    modelState.AddModelError(key, "Enter a valid number.");
                else if ((field.Min.HasValue && number < field.Min) || (field.Max.HasValue && number > field.Max))
                    modelState.AddModelError(key, "Enter a number within the displayed range.");
            }
        }
        // Do not forward unrendered keys or trust submitted template metadata.
        var allowed = form.Fields.Select(field => field.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var key in form.Requirements.Keys.Where(key => !allowed.Contains(key)).ToArray())
            form.Requirements.Remove(key);
    }

    /// <summary>Known safe HTML input presentations; unknown template types are never guessed.</summary>
    public static bool IsSupported(string type) =>
        type is "text" or "textarea" or "email" or "tel" or "url" or "number" or "date" or "select" or "checkbox";
}
