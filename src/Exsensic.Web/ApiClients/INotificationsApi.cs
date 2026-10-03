using Exsensic.Contracts.Notifications;

namespace Exsensic.Web.ApiClients;

/// <summary>
/// Typed client for the notification endpoints (docs/CONTRACTS.md §8): the signed-in user's notifications
/// and marking one as read. Uses the shared <see cref="ApiClient"/>, so the bearer token and error mapping
/// are the same as every other screen.
/// </summary>
public interface INotificationsApi
{
    /// <summary>GET /api/v1/notifications/mine: the user's newest notifications, newest first.</summary>
    Task<ApiResult<List<NotificationDto>>> ListMineAsync(CancellationToken cancellationToken);

    /// <summary>PUT /api/v1/notifications/{id}/read: marks one of the user's notifications as read.</summary>
    Task<ApiResult<object>> MarkReadAsync(int notificationId, CancellationToken cancellationToken);
}

/// <summary>
/// The <see cref="INotificationsApi"/> implementation over the shared JSON transport.
/// </summary>
public sealed class NotificationsApi(ApiClient api) : INotificationsApi
{
    /// <inheritdoc />
    public Task<ApiResult<List<NotificationDto>>> ListMineAsync(CancellationToken cancellationToken) =>
        api.SendAsync<List<NotificationDto>>(HttpMethod.Get, "api/v1/notifications/mine", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<object>> MarkReadAsync(int notificationId, CancellationToken cancellationToken) =>
        api.SendNoContentAsync(HttpMethod.Put, $"api/v1/notifications/{notificationId}/read", cancellationToken);
}
