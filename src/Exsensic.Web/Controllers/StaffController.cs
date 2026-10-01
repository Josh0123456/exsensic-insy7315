using Exsensic.Web.Journeys;
using Exsensic.Web.ApiClients;
using Exsensic.Web.Models.Bookings;
using Exsensic.Web.Models.Staff;
using Exsensic.Web.Models.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Web.Controllers;

/// <summary>Staff-only schedule and assigned booking workspace.</summary>
[Authorize(Roles = "Staff"), Route("Staff")]
public sealed class StaffController(JourneyClock clock, IStaffJourney? journey = null) : JourneyController
{
    /// <summary>Displays the SAST calendar week with previous/next navigation.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(DateOnly? week, CancellationToken cancellationToken)
    {
        var start = clock.WeekStart(clock.WindowStart(week));
        var model = new StaffScheduleViewModel { WeekStart = start, Today = clock.Today };
        return journey is null ? Unavailable("Index", model)
            : await RenderAsync("Index", await journey.ScheduleAsync(start, start.AddDays(6), cancellationToken), model, cancellationToken);
    }

    /// <summary>Displays only information returned by the authorized API detail endpoint.</summary>
    [HttpGet("Bookings/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken) =>
        journey is null ? Unavailable("Details", new BookingDetailViewModel { Id = id })
        : await RenderAsync("Details", await journey.DetailAsync(id, cancellationToken), new BookingDetailViewModel { Id = id }, cancellationToken);

    /// <summary>Requests completion with concurrency state and reloads authoritative API data.</summary>
    [HttpPost("Bookings/{id:guid}/Complete")]
    public async Task<IActionResult> Complete(Guid id, CompleteBookingViewModel model, CancellationToken cancellationToken)
    {
        if (journey is null) { IntegrationError(); return Unavailable("Details", new BookingDetailViewModel { Id = id }); }
        if (ModelState.IsValid)
        {
            var problem = await journey.CompleteAsync(id, model, cancellationToken);
            if (problem is null)
            {
                TempData[ToastKeys.Success] = "The booking has been marked as completed.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var special = await HandleProblemAsync(problem, cancellationToken);
            if (special is not null) return special;
            problem.AddToTempData(TempData);
        }
        // Do not silently retry a stale version; a new GET is an explicit reload.
        return View("ActionFailed", id);
    }
}
