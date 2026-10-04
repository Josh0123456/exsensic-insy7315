using Exsensic.Api.Security;
using Exsensic.Contracts.Notifications;
using Exsensic.Core.Bookings;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Api.Controllers;

/// <summary>
/// The signed-in user's in-app notifications (docs/CONTRACTS.md §6). Any role may use it; the fallback
/// policy already requires a signed-in user. Each user only ever reaches their own notifications.
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly NotificationService _notifications;

    /// <summary>
    /// Creates the controller with the notification service.
    /// </summary>
    public NotificationsController(NotificationService notifications)
    {
        _notifications = notifications;
    }

    /// <summary>The caller's newest 50 notifications, newest first.</summary>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    [HttpGet("mine")]
    public Task<IReadOnlyList<NotificationDto>> Mine(CancellationToken ct) =>
        _notifications.ListMineAsync(User.GetUserId(), ct);

    /// <summary>
    /// Marks one of the caller's notifications as read: 204 No Content, or 404 not_found if it doesn't
    /// exist or belongs to someone else.
    /// </summary>
    /// <param name="id">The notification id.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    [HttpPut("{id:int}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        await _notifications.MarkReadAsync(User.GetUserId(), id, ct);
        return NoContent();
    }
}
