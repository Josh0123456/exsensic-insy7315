using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Common;
using Exsensic.Web.Journeys;
using Exsensic.Web.Models.Admin;
using Exsensic.Web.Models.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Web.Controllers;

/// <summary>Admin booking workflow only; catalogue, time-slot and user administration remain P3-owned.</summary>
[Authorize(Roles = RoleNames.Admin), Route("Admin")]
public sealed class AdminController(IAdminJourney journey) : JourneyController
{
    /// <summary>Displays real summary data, with no fabricated dashboard counts.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        await RenderAsync("Index", await journey.DashboardAsync(cancellationToken), new AdminDashboardViewModel(), cancellationToken);

    /// <summary>Filters real bookings by status and date, preserving filters in pagination.</summary>
    [HttpGet("Bookings")]
    public async Task<IActionResult> Bookings(string? status, DateOnly? from, DateOnly? to, int page = 1, CancellationToken cancellationToken = default)
    {
        if (status is not (null or "" or "Requested" or "Confirmed" or "Completed" or "Cancelled")) status = null;
        page = Math.Max(1, page);
        var model = new AdminBookingListViewModel { Status = status, From = from, To = to, Page = page };
        if (from > to) ModelState.AddModelError(nameof(to), "The end date must be on or after the start date.");
        if (!ModelState.IsValid)
        {
            model.Problem = new ApiClients.ApiProblem { Message = "Check the date filters and try again.", Code = ErrorCodes.ValidationFailed };
            return View(model);
        }
        return await RenderAsync("Bookings", await journey.ListAsync(status, from, to, page, cancellationToken), model, cancellationToken);
    }

    /// <summary>Loads full review data and only API-supplied qualified/free staff choices.</summary>
    [HttpGet("Bookings/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken) =>
        await RenderAsync("Details", await journey.ReviewAsync(id, cancellationToken), new AdminReviewViewModel { Id = id }, cancellationToken);

    /// <summary>Applies the filter form through POST, then redirects to its shareable read-only URL.</summary>
    [HttpPost("Bookings")]
    public IActionResult FilterBookings(string? status, string? from, string? to) =>
        RedirectToAction(nameof(Bookings), new { status, from, to });

    /// <summary>Confirms with the chosen staff member and original RowVersion.</summary>
    [HttpPost("Bookings/{id:int}/Confirm")]
    public async Task<IActionResult> Confirm(int id, [Bind(Prefix = "Confirm")] ConfirmBookingViewModel model, CancellationToken cancellationToken)
    {
        ApiClients.ApiProblem? problem = null;
        if (ModelState.IsValid)
        {
            problem = await journey.ConfirmAsync(id, model, cancellationToken);
            if (problem is null)
            {
                TempData[ToastKeys.Success] = "The booking has been confirmed.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var special = await HandleProblemAsync(problem, cancellationToken, "Confirm");
            if (special is not null) return special;
        }
        if (problem?.Code == ErrorCodes.StaffUnavailable)
        {
            model.StaffUserId = null;
            ModelState.Remove("Confirm.StaffUserId");
        }
        var loaded = await journey.ReviewAsync(id, cancellationToken);
        if (!loaded.IsSuccess || loaded.Value is null)
            return await RenderAsync("Details", loaded, new AdminReviewViewModel { Id = id, Confirm = model }, cancellationToken);
        var page = loaded.Value;
        page.Confirm = model;
        // Neither form may receive a fresh version implicitly after a failed mutation.
        page.Reject.RowVersion = model.RowVersion;
        page.IntegrationAvailable = true;
        page.Problem = problem;
        return View("Details", page);
    }

    /// <summary>Submits a rejection reason; the API owns the Cancelled transition.</summary>
    [HttpPost("Bookings/{id:int}/Reject")]
    public async Task<IActionResult> Reject(int id, [Bind(Prefix = "Reject")] RejectBookingViewModel model, CancellationToken cancellationToken)
    {
        ApiClients.ApiProblem? problem = null;
        if (ModelState.IsValid)
        {
            problem = await journey.RejectAsync(id, model, cancellationToken);
            if (problem is null)
            {
                TempData[ToastKeys.Success] = "The request has been rejected and the booking cancelled.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var special = await HandleProblemAsync(problem, cancellationToken, "Reject");
            if (special is not null) return special;
        }
        var loaded = await journey.ReviewAsync(id, cancellationToken);
        if (!loaded.IsSuccess || loaded.Value is null)
            return await RenderAsync("Details", loaded, new AdminReviewViewModel { Id = id, Reject = model }, cancellationToken);
        var page = loaded.Value;
        page.Reject = model;
        page.Confirm.RowVersion = model.RowVersion;
        page.IntegrationAvailable = true;
        page.Problem = problem;
        return View("Details", page);
    }
}
