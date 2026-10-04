using System.Globalization;
using Exsensic.Contracts.Catalog;
using Exsensic.Web.ApiClients;
using Exsensic.Web.Models.AdminCatalog;
using Exsensic.Web.Models.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Web.Controllers;

/// <summary>
/// Admin time-slot management: list, generate, block, unblock and delete.
/// </summary>
[Authorize(Roles = "Admin")]
[Route("Admin/TimeSlots")]
public sealed class AdminTimeSlotsController : Controller
{
    private readonly IAdminCatalogApi _catalog;

    /// <summary>
    /// Creates the controller with the admin catalogue client.
    /// </summary>
    public AdminTimeSlotsController(IAdminCatalogApi catalog) => _catalog = catalog;

    /// <summary>Lists slots in a date range, defaulting to the next 14 days.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var start = from ?? TodaySast();
        var end = to ?? start.AddDays(13);
        var model = new AdminTimeSlotListViewModel { From = start, To = end };

        var result = await _catalog.ListTimeSlotsAsync(start, end, ct);
        if (!result.IsSuccess || result.Value is null)
        {
            TempData[ToastKeys.Error] = result.Error?.Message ?? "Could not load slots.";
            return View(model);
        }

        model.Slots = result.Value;
        model.IntegrationAvailable = true;
        return View(model);
    }

    /// <summary>Shows the generate form.</summary>
    [HttpGet("Generate")]
    public IActionResult Generate() => View(new AdminTimeSlotGenerateViewModel
    {
        FromDate = TodaySast(),
        ToDate = TodaySast().AddDays(30),
        IntegrationAvailable = true
    });

    /// <summary>Creates slots over a range.</summary>
    [HttpPost("Generate")]
    public async Task<IActionResult> Generate(AdminTimeSlotGenerateViewModel model, CancellationToken ct)
    {
        model.IntegrationAvailable = true;

        var startTimes = ParseStartTimes(model.StartTimesText);
        if (startTimes.Count == 0)
        {
            ModelState.AddModelError(nameof(model.StartTimesText), "Enter at least one time like 09:00, 12:00.");
        }
        if (model.ToDate < model.FromDate)
        {
            ModelState.AddModelError(nameof(model.ToDate), "The end date must be on or after the start date.");
        }
        if (model.Weekdays.Length == 0)
        {
            ModelState.AddModelError(nameof(model.Weekdays), "Choose at least one weekday.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new GenerateSlotsRequest(model.FromDate, model.ToDate, model.Weekdays, startTimes.ToArray(), model.DurationMinutes);
        var result = await _catalog.GenerateSlotsAsync(request, ct);
        if (result.IsSuccess && result.Value is { } generated)
        {
            model.ResultSummary = $"Created {generated.Created} slots, skipped {generated.SkippedDuplicates} duplicates.";
            TempData[ToastKeys.Success] = model.ResultSummary;
            return View(model);
        }

        if (result.Error is not null)
        {
            result.Error.AddToModelState(ModelState);
            model.Problem = result.Error;
        }
        return View(model);
    }

    /// <summary>Blocks a slot with a reason.</summary>
    [HttpPost("{id:int}/Block")]
    public async Task<IActionResult> Block(int id, string reason, string rowVersion, CancellationToken ct)
    {
        var result = await _catalog.BlockSlotAsync(id, new BlockSlotRequest(reason, rowVersion), ct);
        SetToast(result, "Slot blocked.", "Could not block the slot.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Removes a block.</summary>
    [HttpPost("{id:int}/Unblock")]
    public async Task<IActionResult> Unblock(int id, CancellationToken ct)
    {
        var result = await _catalog.UnblockSlotAsync(id, ct);
        SetToast(result, "Slot unblocked.", "Could not unblock the slot.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Deletes a slot that has no active booking.</summary>
    [HttpPost("{id:int}/Delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _catalog.DeleteSlotAsync(id, ct);
        SetToast(result, "Slot deleted.", "Could not delete the slot.");
        return RedirectToAction(nameof(Index));
    }

    private void SetToast(ApiResult<object> result, string success, string fallbackError)
    {
        if (result.IsSuccess)
        {
            TempData[ToastKeys.Success] = success;
        }
        else
        {
            TempData[ToastKeys.Error] = result.Error?.Message ?? fallbackError;
        }
    }

    private static List<TimeOnly> ParseStartTimes(string text)
    {
        var times = new List<TimeOnly>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TimeOnly.TryParseExact(part, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            {
                times.Add(time);
            }
        }
        return times;
    }

    private static DateOnly TodaySast() =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddHours(2));
}
