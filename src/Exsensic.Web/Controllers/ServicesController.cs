using Exsensic.Web.Journeys;
using Exsensic.Web.Models.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Web.Controllers;

/// <summary>Public catalogue presentation using the existing API-backed adapter; no duplicate catalogue client.</summary>
[AllowAnonymous, Route("Services")]
public sealed class ServicesController(ICatalogJourney journey) : JourneyController
{
    /// <summary>Shows real catalogue results or an honest unavailable state.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(string? category, CancellationToken cancellationToken) =>
        await RenderAsync("Index", await journey.ListAsync(category, cancellationToken),
            new ServiceListViewModel { Category = category }, cancellationToken);

    /// <summary>Shows one API-backed service; never fabricates display values from an ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken) =>
        await RenderAsync("Details", await journey.DetailAsync(id, cancellationToken),
            new ServiceDetailViewModel(), cancellationToken);
}
