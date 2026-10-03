using Exsensic.Contracts.Requirements;
using Exsensic.Core.Requirements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Api.Controllers;

/// <summary>
/// The requirement form for a service (docs/CONTRACTS.md §6). Anonymous, like the rest of the
/// catalogue, because the form's questions are not private.
/// </summary>
[ApiController]
[Route("api/v1/services/{serviceId:int}/requirement-template")]
[AllowAnonymous]
public sealed class RequirementTemplatesController : ControllerBase
{
    private readonly RequirementTemplateService _templates;

    /// <summary>
    /// Creates the controller with the service that finds the template.
    /// </summary>
    public RequirementTemplatesController(RequirementTemplateService templates)
    {
        _templates = templates;
    }

    /// <summary>
    /// Returns the requirement template for the service's category, or 404 not_found if the service is
    /// missing or archived.
    /// </summary>
    /// <param name="serviceId">The service being booked.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    [HttpGet]
    public Task<RequirementTemplateDto> Get(int serviceId, CancellationToken ct) =>
        _templates.GetForServiceAsync(serviceId, ct);
}
