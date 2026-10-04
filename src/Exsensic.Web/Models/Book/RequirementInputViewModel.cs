using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Book;

/// <summary>Rendered control metadata mapped from a real template; never serialized to the API.</summary>
public sealed class RequirementInputViewModel
{
    /// <summary>Key for the Web presentation.</summary>
    public string Key { get; set; } = "";

    /// <summary>Label for the Web presentation.</summary>
    public string Label { get; set; } = "";

    /// <summary>InputType for the Web presentation.</summary>
    public string InputType { get; set; } = "text";

    /// <summary>Required for the Web presentation.</summary>
    public bool Required { get; set; }

    /// <summary>MaxLength for the Web presentation.</summary>
    public int? MaxLength { get; set; }

    /// <summary>Min for the Web presentation.</summary>
    public decimal? Min { get; set; }

    /// <summary>Max for the Web presentation.</summary>
    public decimal? Max { get; set; }

    /// <summary>Options for the Web presentation.</summary>
    public IReadOnlyList<string> Options { get; set; } = [];
}
