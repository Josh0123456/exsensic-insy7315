using Exsensic.Web.Journeys;
using Exsensic.Web.ApiClients;
using Exsensic.Web.Models.Book;
using Exsensic.Web.Models.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Web.Controllers;

/// <summary>Two-step Client wizard; availability and creation remain authoritative API operations.</summary>
[Authorize(Roles = "Client"), Route("Book/{serviceId:guid}")]
public sealed class BookController(JourneyClock clock, IBookingWizardJourney? journey = null) : JourneyController
{
    /// <summary>Displays a bounded 14-day availability window.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid serviceId, DateOnly? from, CancellationToken cancellationToken)
    {
        var start = clock.WindowStart(from);
        var model = new SlotStepViewModel { ServiceId = serviceId, From = start };
        return journey is null ? Unavailable("Index", model)
            : await RenderAsync("Index", await journey.SlotsAsync(serviceId, start, null, cancellationToken), model, cancellationToken);
    }

    /// <summary>Retains only the selected identifier, reloading trusted summary data for the next step.</summary>
    [HttpPost("")]
    public async Task<IActionResult> Index(Guid serviceId, SlotStepViewModel model, CancellationToken cancellationToken)
    {
        model.ServiceId = serviceId;
        model.From = clock.WindowStart(model.From);
        if (journey is null)
        {
            IntegrationError();
            return Unavailable("Index", model);
        }
        if (ModelState.IsValid)
            return RedirectToAction(nameof(Details), new { serviceId, timeSlotId = model.TimeSlotId });
        return await RenderAsync("Index", await journey.SlotsAsync(serviceId, model.From, model.TimeSlotId, cancellationToken), model, cancellationToken);
    }

    /// <summary>Loads the selected time, profile and template from real providers.</summary>
    [HttpGet("Details")]
    public async Task<IActionResult> Details(Guid serviceId, string? timeSlotId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(timeSlotId)) return RedirectToAction(nameof(Index), new { serviceId });
        var model = new RequirementsStepViewModel { ServiceId = serviceId, TimeSlotId = timeSlotId };
        return journey is null ? Unavailable("Details", model)
            : await RenderAsync("Details", await journey.RequirementsAsync(serviceId, timeSlotId, cancellationToken), model, cancellationToken);
    }

    /// <summary>Reloads template metadata and submits to the API; never creates local booking state.</summary>
    [HttpPost("Details")]
    public async Task<IActionResult> Details(Guid serviceId, RequirementsStepViewModel model, CancellationToken cancellationToken)
    {
        model.ServiceId = serviceId;
        if (journey is null)
        {
            IntegrationError();
            return Unavailable("Details", model);
        }
        if (string.IsNullOrWhiteSpace(model.TimeSlotId)) return RedirectToAction(nameof(Index), new { serviceId });
        var loaded = await journey.RequirementsAsync(serviceId, model.TimeSlotId, cancellationToken);
        if (!loaded.IsSuccess || loaded.Value is null)
            return await RenderAsync("Details", loaded, model, cancellationToken);
        model.Fields = loaded.Value.Fields;
        model.ServiceName = loaded.Value.ServiceName;
        model.SlotSummary = loaded.Value.SlotSummary;
        model.ContactSummary = loaded.Value.ContactSummary;
        model.IntegrationAvailable = true;
        RequirementForm.Validate(model, ModelState);
        if (ModelState.IsValid)
        {
            var result = await journey.SubmitAsync(model, cancellationToken);
            if (result.IsSuccess && result.HasContent && result.Value != Guid.Empty)
                return RedirectToAction("Confirmation", "Bookings", new { id = result.Value });
            var problem = result.Error ?? new ApiClients.ApiProblem { Message = "The service returned no booking confirmation. Please check My Bookings before trying again." };
            if (problem.Code == "slot_unavailable")
            {
                problem.AddToTempData(TempData);
                return RedirectToAction(nameof(Index), new { serviceId });
            }
            var special = await HandleProblemAsync(problem, cancellationToken);
            if (special is not null) return special;
            model.Problem = problem;
        }
        return View(model);
    }
}
