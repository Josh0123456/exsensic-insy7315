using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Book;

/// <summary>Requirement form plus trusted service, slot and profile display data.</summary>
public sealed class RequirementsStepViewModel : JourneyPage
{
    /// <summary>ServiceId for the Web presentation.</summary>
    public int ServiceId { get; set; }

    /// <summary>TimeSlotId for the Web presentation.</summary>
    [Required]
    public string TimeSlotId { get; set; } = "";

    /// <summary>Requirements for the Web presentation.</summary>
    public Dictionary<string,string> Requirements { get; set; } = new();

    /// <summary>Fields for the Web presentation.</summary>
    [BindNever]
    public IReadOnlyList<RequirementInputViewModel> Fields { get; set; } = [];

    /// <summary>ServiceName for the Web presentation.</summary>
    [BindNever]
    public string? ServiceName { get; set; }

    /// <summary>SlotSummary for the Web presentation.</summary>
    [BindNever]
    public string? SlotSummary { get; set; }

    /// <summary>ContactSummary for the Web presentation.</summary>
    [BindNever]
    public string? ContactSummary { get; set; }
}
