using Exsensic.Contracts.Notifications;
using Exsensic.Core.Abstractions;
using Exsensic.Core.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Exsensic.Core.Bookings;

/// <summary>
/// The signed-in user's in-app notifications, created by the NotificationObserver when bookings change
/// (docs/CONTRACTS.md §12, decision 11). Users only ever see and change their own notifications.
/// </summary>
public sealed class NotificationService
{
    /// <summary>How many of the newest notifications the list returns, so the page and the badge stay quick.</summary>
    public const int MaxNotifications = 50;

    private readonly IAppDbContext _db;

    /// <summary>
    /// Creates the service with the application's database context.
    /// </summary>
    public NotificationService(IAppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// The user's newest notifications, newest first, at most <see cref="MaxNotifications"/>
    /// (GET /api/v1/notifications/mine).
    /// </summary>
    /// <param name="userId">The signed-in user.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    public async Task<IReadOnlyList<NotificationDto>> ListMineAsync(Guid userId, CancellationToken ct) =>
        await _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Take(MaxNotifications)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Message, n.BookingId, n.IsRead, n.CreatedAtUtc))
            .ToListAsync(ct);

    /// <summary>
    /// Marks one of the user's notifications as read (PUT /api/v1/notifications/{id}/read). Marking an
    /// already-read notification again is harmless. Someone else's notification is reported as not found,
    /// exactly like a missing one.
    /// </summary>
    /// <param name="userId">The signed-in user.</param>
    /// <param name="notificationId">The notification to mark.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    /// <exception cref="NotFoundException">The notification doesn't exist or belongs to someone else.</exception>
    public async Task MarkReadAsync(Guid userId, int notificationId, CancellationToken ct)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct)
            ?? throw new NotFoundException("Notification");

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _db.SaveChangesAsync(ct);
        }
    }
}
