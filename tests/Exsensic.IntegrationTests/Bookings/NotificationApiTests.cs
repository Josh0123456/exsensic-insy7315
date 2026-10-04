using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exsensic.Contracts.Enums;
using Exsensic.Contracts.Notifications;

namespace Exsensic.IntegrationTests.Bookings;

/// <summary>
/// GET /notifications/mine and PUT /notifications/{id}/read through the real API: users see only their
/// own notifications, newest first, and can only mark their own as read.
/// </summary>
public sealed class NotificationApiTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    /// <summary>A client's list holds their own notifications, newest first, and nobody else's.</summary>
    [Fact]
    public async Task Mine_TwoClients_ReturnsOnlyOwnNewestFirst()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var (otherClient, _) = await api.CreateSignedInClientAsync();
        var first = await api.BookAsync(client);
        var second = await api.BookAsync(client);
        await api.BookAsync(otherClient);

        var notifications = await client.GetFromJsonAsync<List<NotificationDto>>("/api/v1/notifications/mine");

        Assert.NotNull(notifications);
        Assert.Equal([second.Id, first.Id], notifications.Select(n => n.BookingId));
        Assert.All(notifications, n =>
        {
            Assert.Equal(NotificationType.BookingRequested, n.Type);
            Assert.False(n.IsRead);
        });
        Assert.Contains(first.Reference, notifications[1].Message);
    }

    /// <summary>Marking a notification as read returns 204 and the list then shows it as read; doing it twice is fine.</summary>
    [Fact]
    public async Task MarkRead_OwnNotification_Returns204AndIsRead()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        await api.BookAsync(client);
        var notification = Assert.Single((await client.GetFromJsonAsync<List<NotificationDto>>("/api/v1/notifications/mine"))!);

        var response = await client.PutAsync($"/api/v1/notifications/{notification.Id}/read", null);
        var again = await client.PutAsync($"/api/v1/notifications/{notification.Id}/read", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        var after = await client.GetFromJsonAsync<List<NotificationDto>>("/api/v1/notifications/mine");
        Assert.True(Assert.Single(after!).IsRead);
    }

    /// <summary>Someone else's notification is 404, exactly like one that doesn't exist, and stays unread.</summary>
    [Fact]
    public async Task MarkRead_OtherUsersOrMissing_Returns404()
    {
        var (owner, _) = await api.CreateSignedInClientAsync();
        var (stranger, _) = await api.CreateSignedInClientAsync();
        await api.BookAsync(owner);
        var notification = Assert.Single((await owner.GetFromJsonAsync<List<NotificationDto>>("/api/v1/notifications/mine"))!);

        await AssertProblemAsync(await stranger.PutAsync($"/api/v1/notifications/{notification.Id}/read", null), HttpStatusCode.NotFound, "not_found");
        await AssertProblemAsync(await stranger.PutAsync("/api/v1/notifications/999999/read", null), HttpStatusCode.NotFound, "not_found");
        Assert.False(Assert.Single((await owner.GetFromJsonAsync<List<NotificationDto>>("/api/v1/notifications/mine"))!).IsRead);
    }

    /// <summary>Signed-out callers get 401.</summary>
    [Fact]
    public async Task Mine_Anonymous_Returns401()
    {
        await AssertProblemAsync(await api.CreateAnonymousClient().GetAsync("/api/v1/notifications/mine"), HttpStatusCode.Unauthorized, "unauthenticated");
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}
