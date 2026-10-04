using Exsensic.Web.ApiClients;
using Exsensic.Web.Models.Notifications;
using Exsensic.Web.Models.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Web.Controllers;

/// <summary>
/// The signed-in user's notifications page (docs/CONTRACTS.md §8). Thin: it reads and changes
/// notifications only through the API; there are no business rules in the Web project.
/// </summary>
[Authorize, Route("Notifications")]
public sealed class NotificationsController(INotificationsApi notifications) : JourneyController
{
    /// <summary>Lists the user's notifications, unread ones emphasised, each linking to its booking.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await notifications.ListMineAsync(cancellationToken);
        var page = new NotificationListViewModel();
        if (result.Value is { } items)
        {
            page.Notifications = items;
        }

        return await RenderAsync("Index",
            new ApiResult<NotificationListViewModel>(result.StatusCode, result.Value is null ? null : page, result.Value is not null, result.Error),
            page, cancellationToken);
    }

    /// <summary>
    /// Marks one notification as read and returns to the list. A form post with the global antiforgery
    /// check, so another site can't mark notifications on the user's behalf.
    /// </summary>
    [HttpPost("{id:int}/Read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        var result = await notifications.MarkReadAsync(id, cancellationToken);
        if (result.IsSuccess)
        {
            TempData[ToastKeys.Success] = "Notification marked as read.";
            return RedirectToAction(nameof(Index));
        }

        var special = await HandleProblemAsync(result.Error!, cancellationToken);
        if (special is not null)
        {
            return special;
        }

        TempData[ToastKeys.Error] = result.Error!.Message;
        return RedirectToAction(nameof(Index));
    }
}
