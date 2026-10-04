using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exsensic.Contracts.Admin;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Enums;
using Exsensic.Contracts.Notifications;
using Exsensic.Contracts.Staff;

namespace Exsensic.IntegrationTests.Bookings;

/// <summary>
/// Whole booking journeys through the real API, checking the status, the history timeline and the
/// notifications at every step. The API's clock is moved forward with <see cref="TestClock"/>, so the
/// time rules (complete only after the slot starts, the 24-hour cancellation cut-off) are tested without
/// waiting and give the same result on every run.
/// </summary>
public sealed class BookingLifecycleApiTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    /// <summary>
    /// Create → confirm → complete: Requested, then Confirmed with the staff member named, then Completed
    /// once the slot has started, with one history entry and the right notifications at each step.
    /// </summary>
    [Fact]
    public async Task Lifecycle_CreateConfirmComplete_StatusHistoryAndNotificationsAtEachStep()
    {
        var (client, clientId) = await api.CreateSignedInClientAsync();
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (staff, staffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        var serviceId = await api.AddServiceAsync();
        await api.LinkStaffToServiceAsync(staffId, serviceId);
        var slotDate = api.NextFutureDate();
        var slotId = await api.AddSlotAsync(slotDate, new TimeOnly(9, 0));

        // 1. The client books: Requested.
        var created = await api.BookAsync(client, serviceId, slotId);
        var requested = await BookingApiFactory.GetDetailAsync(client, created.Id);
        Assert.Equal(BookingStatus.Requested, requested.Status);
        AssertTimeline(requested, (null, BookingStatus.Requested));

        // 2. The admin approves with a qualified staff member: Confirmed.
        var confirmed = await SendAsync(admin, HttpMethod.Put, $"/api/v1/admin/bookings/{created.Id}/confirm",
            new ConfirmBookingRequest(staffId, requested.RowVersion));
        Assert.Equal(BookingStatus.Confirmed, confirmed.Status);
        Assert.Equal("Test Staff", confirmed.StaffName);
        AssertTimeline(confirmed, (null, BookingStatus.Requested), (BookingStatus.Requested, BookingStatus.Confirmed));

        // 3. Before the slot starts the staff member can't complete it.
        var early = await staff.PutAsJsonAsync($"/api/v1/staff/bookings/{created.Id}/complete", new CompleteBookingRequest(confirmed.RowVersion));
        await AssertProblemAsync(early, HttpStatusCode.Conflict, "invalid_transition");

        // 4. Move the clock to an hour after the slot starts; now the staff member can complete it.
        api.Clock.SetUtcNow(SlotStartUtc(slotDate, new TimeOnly(9, 0)).AddHours(1));
        var staffNow = await api.SignInAsAsync(staffId, RoleNames.Staff);
        var completed = await SendAsync(staffNow, HttpMethod.Put, $"/api/v1/staff/bookings/{created.Id}/complete",
            new CompleteBookingRequest(confirmed.RowVersion));
        Assert.Equal(BookingStatus.Completed, completed.Status);
        AssertTimeline(completed,
            (null, BookingStatus.Requested),
            (BookingStatus.Requested, BookingStatus.Confirmed),
            (BookingStatus.Confirmed, BookingStatus.Completed));

        // The client heard about each step, and the staff member about their assignment.
        var clientNow = await api.SignInAsAsync(clientId, RoleNames.Client);
        var clientTypes = (await clientNow.GetFromJsonAsync<List<NotificationDto>>("/api/v1/notifications/mine"))!
            .Where(n => n.BookingId == created.Id).Select(n => n.Type).ToList();
        Assert.Equal([NotificationType.BookingCompleted, NotificationType.BookingConfirmed, NotificationType.BookingRequested], clientTypes);
        var staffTypes = (await staffNow.GetFromJsonAsync<List<NotificationDto>>("/api/v1/notifications/mine"))!
            .Where(n => n.BookingId == created.Id).Select(n => n.Type).ToList();
        Assert.Equal([NotificationType.StaffAssigned], staffTypes);

        // 5. A completed booking accepts nothing more.
        var afterEnd = await clientNow.PutAsJsonAsync($"/api/v1/bookings/{created.Id}/cancel", new CancelBookingRequest(null, completed.RowVersion));
        await AssertProblemAsync(afterEnd, HttpStatusCode.Conflict, "invalid_transition");
    }

    /// <summary>
    /// The reject path: the booking ends Cancelled with the "Rejected: " reason, the client is told why, and
    /// the slot is free for someone else.
    /// </summary>
    [Fact]
    public async Task Lifecycle_Reject_CancelsNotifiesAndFreesSlot()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var (otherClient, _) = await api.CreateSignedInClientAsync();
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var serviceId = await api.AddServiceAsync();
        var slotId = await api.AddFutureSlotAsync();
        var created = await api.BookAsync(client, serviceId, slotId);
        var requested = await BookingApiFactory.GetDetailAsync(client, created.Id);

        var rejected = await SendAsync(admin, HttpMethod.Put, $"/api/v1/admin/bookings/{created.Id}/reject",
            new RejectBookingRequest("We are fully booked that week.", requested.RowVersion));

        Assert.Equal(BookingStatus.Cancelled, rejected.Status);
        Assert.Equal("Rejected: We are fully booked that week.", rejected.CancellationReason);
        AssertTimeline(rejected, (null, BookingStatus.Requested), (BookingStatus.Requested, BookingStatus.Cancelled));

        var notice = (await client.GetFromJsonAsync<List<NotificationDto>>("/api/v1/notifications/mine"))!
            .First(n => n.BookingId == created.Id);
        Assert.Equal(NotificationType.BookingRejected, notice.Type);
        Assert.EndsWith("Reason: We are fully booked that week.", notice.Message);

        var rebooked = await api.BookAsync(otherClient, serviceId, slotId);
        Assert.Equal(BookingStatus.Requested, rebooked.Status);
    }

    /// <summary>
    /// The cancellation cut-off with a moving clock: far from the start the client may cancel, but once the
    /// clock is two hours before the start the client is refused and an admin may still cancel.
    /// </summary>
    [Fact]
    public async Task Lifecycle_CancelAsStartApproaches_ClientRefusedInsideCutoffAdminAllowed()
    {
        var (client, clientId) = await api.CreateSignedInClientAsync();
        var (admin, adminId) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (_, staffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        var serviceId = await api.AddServiceAsync();
        await api.LinkStaffToServiceAsync(staffId, serviceId);
        var slotDate = api.NextFutureDate();
        var created = await api.BookAsync(client, serviceId, await api.AddSlotAsync(slotDate, new TimeOnly(9, 0)));
        var confirmed = await SendAsync(admin, HttpMethod.Put, $"/api/v1/admin/bookings/{created.Id}/confirm",
            new ConfirmBookingRequest(staffId, (await BookingApiFactory.GetDetailAsync(client, created.Id)).RowVersion));

        api.Clock.SetUtcNow(SlotStartUtc(slotDate, new TimeOnly(9, 0)).AddHours(-2));
        var clientNow = await api.SignInAsAsync(clientId, RoleNames.Client);
        var adminNow = await api.SignInAsAsync(adminId, RoleNames.Admin);

        var refused = await clientNow.PutAsJsonAsync($"/api/v1/bookings/{created.Id}/cancel", new CancelBookingRequest("Too late?", confirmed.RowVersion));
        await AssertProblemAsync(refused, HttpStatusCode.Conflict, "cancel_window_closed");

        var cancelled = await SendAsync(adminNow, HttpMethod.Put, $"/api/v1/bookings/{created.Id}/cancel",
            new CancelBookingRequest("Client phoned in sick.", confirmed.RowVersion));
        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
        Assert.Equal("Test Admin", cancelled.History[^1].ChangedByName);
    }

    /// <summary>When the slot starts, as a UTC time (slots are stored in SAST, UTC+2).</summary>
    private static DateTimeOffset SlotStartUtc(DateOnly date, TimeOnly start) =>
        new DateTimeOffset(date.ToDateTime(start), TimeSpan.FromHours(2)).ToUniversalTime();

    /// <summary>Checks the timeline holds exactly these from → to steps, oldest first.</summary>
    private static void AssertTimeline(BookingDetailDto booking, params (BookingStatus? From, BookingStatus To)[] expected) =>
        Assert.Equal(expected, booking.History.Select(h => (h.FromStatus, h.ToStatus)));

    /// <summary>Sends a JSON request that must succeed with 200, and returns the updated booking.</summary>
    private static async Task<BookingDetailDto> SendAsync<T>(HttpClient client, HttpMethod method, string url, T body)
    {
        var response = await client.SendAsync(new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookingDetailDto>())!;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}
