using Exsensic.Web.ApiClients;
using Exsensic.Web.Journeys;
using Exsensic.Web.Models.Bookings;
using Exsensic.Web.Models.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Web.Controllers;

/// <summary>Client booking views and mutations; all persisted data and decisions belong to the API.</summary>
[Authorize(Roles = "Client"), Route("Bookings")]
public sealed class BookingsController(JourneyClock clock, IBookingJourney? journey = null) : JourneyController
{
    /// <summary>Displays UI groupings without inventing stored booking statuses.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(string filter = "Upcoming", int page = 1, CancellationToken cancellationToken = default)
    {
        if (filter is not ("Upcoming" or "Requested" or "Confirmed" or "Past" or "Cancelled")) filter = "Upcoming";
        page = Math.Max(1, page);
        var model = new BookingListViewModel { Filter = filter, Page = page };
        return journey is null ? Unavailable("Index", model)
            : await RenderAsync("Index", await journey.MineAsync(filter, page, cancellationToken), model, cancellationToken);
    }

    /// <summary>Loads the caller's booking; API 404 never reveals ownership.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken) =>
        journey is null ? Unavailable("Details", new BookingDetailViewModel { Id = id })
        : await RenderAsync("Details", await journey.DetailAsync(id, cancellationToken), new BookingDetailViewModel { Id = id }, cancellationToken);

    /// <summary>Reloads the real booking before displaying any confirmation information.</summary>
    [HttpGet("{id:guid}/Confirmation")]
    public async Task<IActionResult> Confirmation(Guid id, CancellationToken cancellationToken) =>
        journey is null ? Unavailable("Confirmation", new BookingDetailViewModel { Id = id })
        : await RenderAsync("Confirmation", await journey.DetailAsync(id, cancellationToken), new BookingDetailViewModel { Id = id }, cancellationToken);

    /// <summary>Displays real available times for the booking's own service.</summary>
    [HttpGet("{id:guid}/Reschedule")]
    public async Task<IActionResult> Reschedule(Guid id, DateOnly? from, CancellationToken cancellationToken)
    {
        var start = clock.WindowStart(from);
        var model = new RescheduleViewModel { Id = id, From = start };
        return journey is null ? Unavailable("Reschedule", model)
            : await RenderAsync("Reschedule", await journey.ReschedulePageAsync(id, start, null, cancellationToken), model, cancellationToken);
    }

    /// <summary>Submits the original concurrency token; conflicts require an explicit reload.</summary>
    [HttpPost("{id:guid}/Reschedule")]
    public async Task<IActionResult> Reschedule(Guid id, RescheduleViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        model.From = clock.WindowStart(model.From);
        if (journey is null) { IntegrationError(); return Unavailable("Reschedule", model); }
        ApiProblem? problem = null;
        if (ModelState.IsValid)
        {
            problem = await journey.RescheduleAsync(id, model, cancellationToken);
            if (problem is null)
            {
                TempData[ToastKeys.Success] = "Your booking time has been updated. Please review its current status.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var special = await HandleProblemAsync(problem, cancellationToken);
            if (special is not null) return special;
        }
        if (problem?.Code == "slot_unavailable")
        {
            model.NewTimeSlotId = null;
            ModelState.Remove(nameof(model.NewTimeSlotId));
        }
        var loaded = await journey.ReschedulePageAsync(id, model.From, model.NewTimeSlotId, cancellationToken);
        if (!loaded.IsSuccess || loaded.Value is null) return await RenderAsync("Reschedule", loaded, model, cancellationToken);
        model.Detail = loaded.Value.Detail;
        model.Picker = loaded.Value.Picker;
        model.IntegrationAvailable = true;
        model.Problem = problem;
        return View(model);
    }

    /// <summary>Shows a non-destructive cancellation confirmation page.</summary>
    [HttpGet("{id:guid}/Cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken) =>
        journey is null ? Unavailable("Cancel", new CancelViewModel { Id = id })
        : await RenderAsync("Cancel", await journey.CancelPageAsync(id, cancellationToken), new CancelViewModel { Id = id }, cancellationToken);

    /// <summary>Asks the API to cancel; never computes the cancellation window locally.</summary>
    [HttpPost("{id:guid}/Cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancelViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        if (journey is null) { IntegrationError(); return Unavailable("Cancel", model); }
        ApiProblem? problem = null;
        if (ModelState.IsValid)
        {
            problem = await journey.CancelAsync(id, model, cancellationToken);
            if (problem is null)
            {
                TempData[ToastKeys.Success] = "Your booking has been cancelled.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var special = await HandleProblemAsync(problem, cancellationToken);
            if (special is not null) return special;
        }
        var loaded = await journey.CancelPageAsync(id, cancellationToken);
        if (!loaded.IsSuccess || loaded.Value is null) return await RenderAsync("Cancel", loaded, model, cancellationToken);
        model.Detail = loaded.Value.Detail;
        model.CancellationCutoffHours = loaded.Value.CancellationCutoffHours;
        model.IntegrationAvailable = true;
        model.Problem = problem;
        return View(model);
    }
}
