using Exsensic.Web.ApiClients;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Exsensic.Web.Models.Journeys;

/// <summary>Page presentation state: missing integration is distinct from an empty API result.</summary>
public abstract class JourneyPage
{
    /// <summary>Set only by the controller after a real integration is available.</summary>
    [BindNever]
    public bool IntegrationAvailable { get; set; }

    /// <summary>Safe feedback from the shared API error mapper.</summary>
    [BindNever]
    public ApiProblem? Problem { get; set; }
}
