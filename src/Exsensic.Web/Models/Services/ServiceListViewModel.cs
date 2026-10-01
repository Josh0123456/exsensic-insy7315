using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Services;

/// <summary>Catalogue layout populated only by the future adapter to P3's catalogue client.</summary>
public sealed class ServiceListViewModel : JourneyPage
{
    /// <summary>Category for the Web presentation.</summary>
    public string? Category { get; set; }

    /// <summary>Categories for the Web presentation.</summary>
    [BindNever]
    public IReadOnlyList<string> Categories { get; set; } = [];

    /// <summary>Services for the Web presentation.</summary>
    [BindNever]
    public IReadOnlyList<ServiceCardViewModel> Services { get; set; } = [];
}
