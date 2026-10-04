using Exsensic.Api.Security;
using Exsensic.Contracts.Catalog;
using Exsensic.Core.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Api.Controllers;

/// <summary>
/// Admin-only time-slot management.
/// </summary>
[ApiController]
[Route("api/v1/admin/timeslots")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminTimeSlotsController : ControllerBase
{
    private readonly ServiceCatalogService _catalog;

    /// <summary>
    /// Creates the controller with the catalogue service.
    /// </summary>
    public AdminTimeSlotsController(ServiceCatalogService catalog) => _catalog = catalog;

    /// <summary>Slots in a date range.</summary>
    [HttpGet]
    public Task<IReadOnlyList<TimeSlotDto>> List(DateOnly? from, DateOnly? to, CancellationToken ct) =>
        _catalog.ListTimeSlotsAsync(from, to, ct);

    /// <summary>Generates slots over a date range.</summary>
    [HttpPost("generate")]
    public Task<GenerateSlotsResult> Generate(GenerateSlotsRequest request, CancellationToken ct) =>
        _catalog.GenerateSlotsAsync(request, ct);

    /// <summary>Blocks a slot with a reason.</summary>
    [HttpPut("{id:int}/block")]
    public async Task<IActionResult> Block(int id, BlockSlotRequest request, CancellationToken ct)
    {
        await _catalog.BlockSlotAsync(id, request.Reason, request.RowVersion, ct);
        return NoContent();
    }

    /// <summary>Removes a block.</summary>
    [HttpPut("{id:int}/unblock")]
    public async Task<IActionResult> Unblock(int id, CancellationToken ct)
    {
        await _catalog.UnblockSlotAsync(id, ct);
        return NoContent();
    }

    /// <summary>Deletes a slot with no active booking.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _catalog.DeleteSlotAsync(id, ct);
        return NoContent();
    }
}
