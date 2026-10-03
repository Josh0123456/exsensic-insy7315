using Exsensic.Contracts.Catalog;
using Exsensic.Core.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Api.Controllers;

/// <summary>The public service catalogue and availability (docs/CONTRACTS.md §6). No sign-in needed.</summary>
[ApiController]
[Route("api/v1/services")]
[AllowAnonymous]
public sealed class ServicesController(ServiceCatalogService catalog, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Active services, ordered by category then name.</summary>
    [HttpGet]
    public Task<IReadOnlyList<ServiceDto>> List(CancellationToken ct) => catalog.ListActiveAsync(ct);

    /// <summary>One active service, or 404 not_found if it is missing or archived.</summary>
    [HttpGet("{id:int}")]
    public Task<ServiceDto> Get(int id, CancellationToken ct) => catalog.GetActiveAsync(id, ct);

    /// <summary>
    /// Bookable slots for the service between from and to (inclusive). Defaults to the next 14 days;
    /// a range longer than 60 days, or ending before it starts, is 400 validation_failed.
    /// </summary>
    [HttpGet("{id:int}/availability")]
    public async Task<ActionResult<IReadOnlyList<AvailableSlotDto>>> Availability(
        int id, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var start = from ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().AddHours(2).DateTime);
        var end = to ?? start.AddDays(13);

        if (end < start)
        {
            ModelState.AddModelError(nameof(to), "The end date must be on or after the start date.");
        }
        else if (end.DayNumber - start.DayNumber + 1 > ServiceCatalogService.MaxAvailabilityDays)
        {
            ModelState.AddModelError(nameof(to), $"Ask for at most {ServiceCatalogService.MaxAvailabilityDays} days at a time.");
        }

        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Ok(await catalog.AvailabilityAsync(id, start, end, ct));
    }
}
