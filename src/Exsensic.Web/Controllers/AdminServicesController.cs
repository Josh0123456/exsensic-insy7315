using Exsensic.Contracts.Catalog;
using Exsensic.Web.ApiClients;
using Exsensic.Web.Models.AdminCatalog;
using Exsensic.Web.Models.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Web.Controllers;

/// <summary>
/// Admin service management: list, create, edit, archive and restore.
/// The API decides every rule; this controller only shapes the request and the response.
/// </summary>
[Authorize(Roles = "Admin")]
[Route("Admin/Services")]
public sealed class AdminServicesController : Controller
{
    private readonly IAdminCatalogApi _catalog;

    /// <summary>
    /// Creates the controller with the admin catalogue client.
    /// </summary>
    public AdminServicesController(IAdminCatalogApi catalog) => _catalog = catalog;

    /// <summary>Lists every service, active or not.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var result = await _catalog.ListServicesAsync(ct);
        if (!result.IsSuccess || result.Value is null)
        {
            TempData[ToastKeys.Error] = result.Error?.Message ?? "Could not load services.";
            return View(new AdminServiceListViewModel { IntegrationAvailable = false });
        }

        return View(new AdminServiceListViewModel
        {
            Services = result.Value,
            IntegrationAvailable = true
        });
    }

    /// <summary>Shows the empty create form.</summary>
    [HttpGet("Create")]
    public IActionResult Create() => View(new AdminServiceFormViewModel { IntegrationAvailable = true });

    /// <summary>Creates a service.</summary>
    [HttpPost("Create")]
    public async Task<IActionResult> Create(AdminServiceFormViewModel model, CancellationToken ct)
    {
        model.IntegrationAvailable = true;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new SaveServiceRequest(model.Name, model.Category, model.Description, model.DurationMinutes, model.BasePrice);
        var result = await _catalog.CreateServiceAsync(request, ct);
        if (result.IsSuccess)
        {
            TempData[ToastKeys.Success] = $"Service \"{model.Name}\" created.";
            return RedirectToAction(nameof(Index));
        }

        AddProblem(result, model);
        return View(model);
    }

    /// <summary>Shows the edit form for a service.</summary>
    [HttpGet("{id:int}/Edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var result = await _catalog.ListServicesAsync(ct);
        var service = result.Value?.FirstOrDefault(s => s.Id == id);
        if (service is null)
        {
            Response.StatusCode = 404;
            return View("~/Views/Shared/NotFound.cshtml");
        }

        return View(new AdminServiceFormViewModel
        {
            Id = service.Id,
            Name = service.Name,
            Category = service.Category,
            Description = service.Description,
            DurationMinutes = service.DurationMinutes,
            BasePrice = service.BasePrice,
            RowVersion = service.RowVersion,
            IntegrationAvailable = true
        });
    }

    /// <summary>Saves the edit.</summary>
    [HttpPost("{id:int}/Edit")]
    public async Task<IActionResult> Edit(int id, AdminServiceFormViewModel model, CancellationToken ct)
    {
        model.Id = id;
        model.IntegrationAvailable = true;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new SaveServiceRequest(model.Name, model.Category, model.Description, model.DurationMinutes, model.BasePrice);
        var result = await _catalog.UpdateServiceAsync(id, request, ct);
        if (result.IsSuccess)
        {
            TempData[ToastKeys.Success] = "Service updated.";
            return RedirectToAction(nameof(Index));
        }

        AddProblem(result, model);
        return View(model);
    }

    /// <summary>Archives or restores a service.</summary>
    [HttpPost("{id:int}/Toggle")]
    public async Task<IActionResult> Toggle(int id, string rowVersion, bool isActive, CancellationToken ct)
    {
        var result = await _catalog.SetServiceActiveAsync(id, new SetActiveRequest(isActive, rowVersion), ct);
        if (result.IsSuccess)
        {
            TempData[ToastKeys.Success] = isActive ? "Service restored." : "Service archived.";
        }
        else
        {
            TempData[ToastKeys.Error] = result.Error?.Message ?? "Could not change the service.";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Copies API field errors onto ModelState so the form shows them beside the right fields.
    /// </summary>
    private void AddProblem(ApiResult<object> result, AdminServiceFormViewModel model)
    {
        if (result.Error is null)
        {
            ModelState.AddModelError(string.Empty, "The service could not be saved.");
            return;
        }

        result.Error.AddToModelState(ModelState);
        model.Problem = result.Error;
    }
}
