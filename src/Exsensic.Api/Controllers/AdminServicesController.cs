using Exsensic.Api.Security;
using Exsensic.Contracts.Catalog;
using Exsensic.Core.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Api.Controllers;

/// <summary>
/// Admin-only service catalogue management.
/// </summary>
[ApiController]
[Route("api/v1/admin/services")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminServicesController : ControllerBase
{
    private readonly ServiceCatalogService _catalog;

    /// <summary>
    /// Creates the controller with the catalogue service.
    /// </summary>
    public AdminServicesController(ServiceCatalogService catalog) => _catalog = catalog;

    /// <summary>All services, active or not.</summary>
    [HttpGet]
    public Task<IReadOnlyList<ServiceDto>> List(CancellationToken ct) => _catalog.ListAllAsync(ct);

    /// <summary>Creates a new service.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(SaveServiceRequest request, CancellationToken ct)
    {
        var id = await _catalog.CreateAsync(request, ct);
        return Created($"/api/v1/services/{id}", null);
    }

    /// <summary>Updates an existing service.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveServiceRequest request, CancellationToken ct)
    {
        await _catalog.UpdateAsync(id, request, ct);
        return NoContent();
    }

    /// <summary>Archives or restores a service.</summary>
    [HttpPut("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, SetActiveRequest request, CancellationToken ct)
    {
        await _catalog.SetActiveAsync(id, request.IsActive, request.RowVersion, ct);
        return NoContent();
    }
}
