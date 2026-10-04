using Exsensic.Contracts.Admin;
using Exsensic.Web.ApiClients;
using Exsensic.Web.Models.AdminCatalog;
using Exsensic.Web.Models.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Web.Controllers;

/// <summary>
/// Admin user management: list, create staff, activate or deactivate accounts.
/// </summary>
[Authorize(Roles = "Admin")]
[Route("Admin/Users")]
public sealed class AdminUsersController : Controller
{
    private readonly IAdminUsersApi _users;
    private readonly IAdminCatalogApi _catalog;

    /// <summary>
    /// Creates the controller with the user and catalogue clients.
    /// </summary>
    public AdminUsersController(IAdminUsersApi users, IAdminCatalogApi catalog)
    {
        _users = users;
        _catalog = catalog;
    }

    /// <summary>Lists users, with an optional role filter and paging.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(string? role, int page, CancellationToken ct)
    {
        page = Math.Max(1, page);
        var model = new AdminUsersViewModel { Role = role, Page = page };
        var result = await _users.ListAsync(role, page, ct);
        if (!result.IsSuccess || result.Value is null)
        {
            TempData[ToastKeys.Error] = result.Error?.Message ?? "Could not load users.";
            return View(model);
        }

        model.Users = result.Value.Items;
        model.TotalCount = result.Value.TotalCount;
        model.IntegrationAvailable = true;
        return View(model);
    }

    /// <summary>Shows the create-staff form.</summary>
    [HttpGet("Create")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var services = await _catalog.ListServicesAsync(ct);
        return View(new AdminCreateStaffViewModel
        {
            AvailableServices = services.Value ?? [],
            IntegrationAvailable = true
        });
    }

    /// <summary>Creates a Staff account.</summary>
    [HttpPost("Create")]
    public async Task<IActionResult> Create(AdminCreateStaffViewModel model, CancellationToken ct)
    {
        model.IntegrationAvailable = true;
        if (!ModelState.IsValid)
        {
            var services = await _catalog.ListServicesAsync(ct);
            model.AvailableServices = services.Value ?? [];
            return View(model);
        }

        var request = new CreateStaffRequest(model.FullName, model.Email, model.Password, model.JobTitle, model.ServiceIds);
        var result = await _users.CreateStaffAsync(request, ct);
        if (result.IsSuccess)
        {
            TempData[ToastKeys.Success] = $"Staff account for {model.FullName} created.";
            return RedirectToAction(nameof(Index));
        }

        if (result.Error is not null)
        {
            result.Error.AddToModelState(ModelState);
            model.Problem = result.Error;
        }
        var reloaded = await _catalog.ListServicesAsync(ct);
        model.AvailableServices = reloaded.Value ?? [];
        return View(model);
    }

    /// <summary>Activates or deactivates a user account.</summary>
    [HttpPost("{id:guid}/Toggle")]
    public async Task<IActionResult> Toggle(Guid id, bool isActive, CancellationToken ct)
    {
        var result = await _users.SetActiveAsync(id, new SetUserActiveRequest(isActive), ct);
        if (result.IsSuccess)
        {
            TempData[ToastKeys.Success] = isActive ? "User activated." : "User deactivated.";
        }
        else
        {
            TempData[ToastKeys.Error] = result.Error?.Message ?? "Could not change the user.";
        }

        return RedirectToAction(nameof(Index));
    }
}
