using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Services;

/// <summary>Service detail composition; no substitute service DTO.</summary>
public sealed class ServiceDetailViewModel : JourneyPage
{
    /// <summary>Service for the Web presentation.</summary>
    [BindNever]
    public ServiceCardViewModel? Service { get; set; }
}
