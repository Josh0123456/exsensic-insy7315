using Exsensic.Contracts.Notifications;
using Exsensic.Web.Models.Journeys;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Exsensic.Web.Models.Notifications;

/// <summary>The /Notifications page: the signed-in user's notifications, newest first.</summary>
public sealed class NotificationListViewModel : JourneyPage
{
    /// <summary>The notifications returned by the API.</summary>
    [BindNever]
    public IReadOnlyList<NotificationDto> Notifications { get; set; } = [];

    /// <summary>How many of them are unread, for the page subtitle.</summary>
    public int UnreadCount => Notifications.Count(n => !n.IsRead);
}
