using Exsensic.Web.Journeys;
using Exsensic.Web.Models.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Web.Controllers;

/// <summary>Public catalogue presentation; the adapter must use P3's catalogue client.</summary>
[AllowAnonymous, Route("Services")]
public sealed class ServicesController(ICatalogJourney? journey = null) : JourneyController
{
    /// <summary>Shows real catalogue results or an honest unavailable state.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(string? category, CancellationToken cancellationToken) =>
        journey is null ? Unavailable("Index", new ServiceListViewModel { Category = category })
        : await RenderAsync("Index", await journey.ListAsync(category, cancellationToken),
            new ServiceListViewModel { Category = category }, cancellationToken);

    /// <summary>Shows one API-backed service; never fabricates display values from an ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken) =>
        journey is null ? Unavailable("Details", new ServiceDetailViewModel())
        : await RenderAsync("Details", await journey.DetailAsync(id, cancellationToken),
            new ServiceDetailViewModel(), cancellationToken);
}
