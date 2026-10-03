using Exsensic.Web.ApiClients;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Web.ViewComponents;

/// <summary>
/// The "Notifications" link in the main navigation, with the number of unread notifications
/// (docs/CONTRACTS.md §8). Shown only to signed-in users. The count is hidden when it is zero, and screen
/// readers hear it as words ("3 unread notifications"), not just a number.
/// </summary>
public sealed class NotificationBadgeViewComponent(INotificationsApi notifications) : ViewComponent
{
    /// <summary>
    /// Reads the user's notifications from the API and renders the link with the unread count. If the API
    /// can't be reached the link is still shown without a count, so the navigation never breaks.
    /// </summary>
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Content(string.Empty);
        }

        var result = await notifications.ListMineAsync(HttpContext.RequestAborted);
        var unread = result.Value?.Count(n => !n.IsRead) ?? 0;
        return View(unread);
    }
}
