using Exsensic.Contracts.Enums;

namespace Exsensic.Contracts.Notifications;

/// <summary>
/// One in-app notification, returned by GET /api/v1/notifications/mine (docs/CONTRACTS.md §5–6).
/// </summary>
/// <param name="Id">The notification's id, used to mark it as read.</param>
/// <param name="Type">What the notification is about.</param>
/// <param name="Message">The plain-text message shown to the user.</param>
/// <param name="BookingId">The booking it links to, or null if it isn't about a booking.</param>
/// <param name="IsRead">Whether the user has marked it as read.</param>
/// <param name="CreatedAtUtc">When it was created, in UTC.</param>
public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Message,
    Guid? BookingId,
    bool IsRead,
    DateTimeOffset CreatedAtUtc);
