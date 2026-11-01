using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exsensic.Contracts.Admin;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Enums;
using Exsensic.Contracts.Notifications;
using Exsensic.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Exsensic.IntegrationTests.Bookings;

/// <summary>
/// Fills the gaps left by the journey tests in <see cref="BookingLifecycleApiTests"/>: the history row and
/// the notifications for the reschedule and cancel transitions, stale RowVersions on cancel and reject, and
/// races between two users that must end with exactly one winner and a database that agrees with the
/// responses. Every case goes through the real API.
/// </summary>
public sealed class BookingTransitionEvidenceTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    /// <summary>
    /// A client moves a Confirmed booking: it returns to Requested with a history row, the assigned staff
    /// member is told, and the client is not notified about their own action.
    /// </summary>
    [Fact]
    public async Task Reschedule_ConfirmedByClient_RecordsHistoryAndNotifiesStaffOnly()
    {
        var (client, _, booking, staffId, _) = await ConfirmedBookingAsync();
        var staff = await api.SignInAsAsync(staffId, RoleNames.Staff);
        var clientNoticesBefore = (await NoticesAsync(client, booking.Id)).Count;
        var before = await BookingApiFactory.GetDetailAsync(client, booking.Id);

        var response = await client.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/reschedule",
            new RescheduleBookingRequest(await api.AddFutureSlotAsync(), before.RowVersion));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = (await response.Content.ReadFromJsonAsync<BookingDetailDto>())!;
        Assert.Equal(BookingStatus.Requested, after.Status);
        Assert.Equal(
            [(null, BookingStatus.Requested), (BookingStatus.Requested, BookingStatus.Confirmed), (BookingStatus.Confirmed, BookingStatus.Requested)],
            after.History.Select(h => (h.FromStatus, h.ToStatus)));
        Assert.Equal("Test Client", after.History[^1].ChangedByName);

        Assert.Equal(NotificationType.BookingRescheduled, (await NoticesAsync(staff, booking.Id))[0].Type);
        Assert.Equal(clientNoticesBefore, (await NoticesAsync(client, booking.Id)).Count);
    }

    /// <summary>An admin moves a Confirmed booking: the client hears that it moved and needs approval again.</summary>
    [Fact]
    public async Task Reschedule_ConfirmedByAdmin_NotifiesClient()
    {
        var (client, _, booking, _, admin) = await ConfirmedBookingAsync();
        var before = await BookingApiFactory.GetDetailAsync(client, booking.Id);

        var response = await admin.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/reschedule",
            new RescheduleBookingRequest(await api.AddFutureSlotAsync(), before.RowVersion));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var latest = (await NoticesAsync(client, booking.Id))[0];
        Assert.Equal(NotificationType.BookingRescheduled, latest.Type);
        Assert.Contains("waiting for approval", latest.Message);
    }

    /// <summary>A client cancels a Confirmed booking outside the cut-off: history is written and the staff member is told.</summary>
    [Fact]
    public async Task Cancel_ConfirmedByClient_RecordsHistoryAndNotifiesStaff()
    {
        var (client, _, booking, staffId, _) = await ConfirmedBookingAsync();
        var staff = await api.SignInAsAsync(staffId, RoleNames.Staff);
        var before = await BookingApiFactory.GetDetailAsync(client, booking.Id);

        var response = await client.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel", new CancelBookingRequest("Plans changed.", before.RowVersion));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = (await response.Content.ReadFromJsonAsync<BookingDetailDto>())!;
        Assert.Equal((BookingStatus.Confirmed, BookingStatus.Cancelled), (after.History[^1].FromStatus!.Value, after.History[^1].ToStatus));
        Assert.Equal("Plans changed.", after.History[^1].Note);
        Assert.Equal(NotificationType.BookingCancelled, (await NoticesAsync(staff, booking.Id))[0].Type);
    }

    /// <summary>An admin cancels a Confirmed booking: both the client and the assigned staff member are told.</summary>
    [Fact]
    public async Task Cancel_ConfirmedByAdmin_NotifiesClientAndStaff()
    {
        var (client, _, booking, staffId, admin) = await ConfirmedBookingAsync();
        var staff = await api.SignInAsAsync(staffId, RoleNames.Staff);
        var before = await BookingApiFactory.GetDetailAsync(client, booking.Id);

        var response = await admin.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel", new CancelBookingRequest("Studio closed.", before.RowVersion));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(NotificationType.BookingCancelled, (await NoticesAsync(client, booking.Id))[0].Type);
        Assert.Equal(NotificationType.BookingCancelled, (await NoticesAsync(staff, booking.Id))[0].Type);
    }

    /// <summary>A stale RowVersion on cancel is refused with 409 concurrency_conflict, and the booking is untouched.</summary>
    [Fact]
    public async Task Cancel_StaleRowVersion_Returns409AndChangesNothing()
    {
        var (client, _, booking, _, admin) = await ConfirmedBookingAsync();
        var stale = await BookingApiFactory.GetDetailAsync(client, booking.Id);
        var moved = await admin.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/reschedule",
            new RescheduleBookingRequest(await api.AddFutureSlotAsync(), stale.RowVersion));
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        var response = await client.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel", new CancelBookingRequest(null, stale.RowVersion));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "concurrency_conflict");
        Assert.Equal(BookingStatus.Requested, (await BookingApiFactory.GetDetailAsync(client, booking.Id)).Status);
    }

    /// <summary>A stale RowVersion on reject is refused with 409 concurrency_conflict, and the booking stays Requested.</summary>
    [Fact]
    public async Task Reject_StaleRowVersion_Returns409AndChangesNothing()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var slotId = await api.AddFutureSlotAsync();
        var booking = await api.BookAsync(client, slotId: slotId);
        var stale = await BookingApiFactory.GetDetailAsync(client, booking.Id);
        var moved = await client.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/reschedule",
            new RescheduleBookingRequest(await api.AddFutureSlotAsync(), stale.RowVersion));
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/bookings/{booking.Id}/reject", new RejectBookingRequest("Fully booked.", stale.RowVersion));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "concurrency_conflict");
        Assert.Equal(BookingStatus.Requested, (await BookingApiFactory.GetDetailAsync(client, booking.Id)).Status);
    }

    /// <summary>
    /// The exact two-user race: both book the same free slot at the same moment. One gets 201, the other
    /// 409 slot_unavailable, and the database holds a single active booking for the slot.
    /// </summary>
    [Fact]
    public async Task PostBookings_TwoUsersSameSlot_OneCreatedOneConflict()
    {
        var serviceId = await api.AddServiceAsync();
        var slotId = await api.AddFutureSlotAsync();
        var (first, _) = await api.CreateSignedInClientAsync();
        var (second, _) = await api.CreateSignedInClientAsync();
        var request = new CreateBookingRequest(serviceId, slotId, new Dictionary<string, string>
        {
            ["shootType"] = "Product",
            ["location"] = "Studio",
            ["quantity"] = "5",
            ["description"] = "Two user race.",
        });

        var responses = await RunTogetherAsync(
            () => first.PostAsJsonAsync("/api/v1/bookings", request),
            () => second.PostAsJsonAsync("/api/v1/bookings", request));

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], responses.Select(r => r.StatusCode).Order());
        await AssertProblemAsync(responses.Single(r => r.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict, "slot_unavailable");
        Assert.Equal(1, await ActiveBookingsInSlotAsync(slotId));
    }

    /// <summary>
    /// Two clients move different bookings into the same free slot at the same moment. One succeeds, the
    /// other gets 409 slot_unavailable, and only one active booking holds the slot.
    /// </summary>
    [Fact]
    public async Task Reschedule_TwoBookingsIntoSameSlot_OneMovesOneConflicts()
    {
        var serviceId = await api.AddServiceAsync();
        var (first, _) = await api.CreateSignedInClientAsync();
        var (second, _) = await api.CreateSignedInClientAsync();
        var bookingA = await api.BookAsync(first, serviceId);
        var bookingB = await api.BookAsync(second, serviceId);
        var versionA = (await BookingApiFactory.GetDetailAsync(first, bookingA.Id)).RowVersion;
        var versionB = (await BookingApiFactory.GetDetailAsync(second, bookingB.Id)).RowVersion;
        var target = await api.AddFutureSlotAsync();

        var responses = await RunTogetherAsync(
            () => first.PutAsJsonAsync($"/api/v1/bookings/{bookingA.Id}/reschedule", new RescheduleBookingRequest(target, versionA)),
            () => second.PutAsJsonAsync($"/api/v1/bookings/{bookingB.Id}/reschedule", new RescheduleBookingRequest(target, versionB)));

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], responses.Select(r => r.StatusCode).Order());
        await AssertProblemAsync(responses.Single(r => r.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict, "slot_unavailable");
        Assert.Equal(1, await ActiveBookingsInSlotAsync(target));
    }

    /// <summary>
    /// Two admins confirm the same Requested booking at the same moment with the same RowVersion. One wins;
    /// the other is told 409. The history holds exactly one confirmation.
    /// </summary>
    [Fact]
    public async Task Confirm_TwoAdminsSameRowVersion_OneConfirmsOneConflicts()
    {
        var serviceId = await api.AddServiceAsync();
        var (client, _) = await api.CreateSignedInClientAsync();
        var (adminA, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (adminB, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (_, staffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        await api.LinkStaffToServiceAsync(staffId, serviceId);
        var booking = await api.BookAsync(client, serviceId);
        var version = (await BookingApiFactory.GetDetailAsync(client, booking.Id)).RowVersion;
        var request = new ConfirmBookingRequest(staffId, version);

        var responses = await RunTogetherAsync(
            () => adminA.PutAsJsonAsync($"/api/v1/admin/bookings/{booking.Id}/confirm", request),
            () => adminB.PutAsJsonAsync($"/api/v1/admin/bookings/{booking.Id}/confirm", request));

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], responses.Select(r => r.StatusCode).Order());
        var detail = await BookingApiFactory.GetDetailAsync(client, booking.Id);
        Assert.Equal(BookingStatus.Confirmed, detail.Status);
        Assert.Equal(1, detail.History.Count(h => h.ToStatus == BookingStatus.Confirmed));
    }

    /// <summary>
    /// A client cancels while an admin confirms, both holding the same RowVersion. Whichever lands first
    /// wins; the other gets 409, and the stored status and history agree with the winner. Repeated, because a
    /// single run might not overlap.
    /// </summary>
    [Fact]
    public async Task CancelAndConfirm_SameRowVersion_ExactlyOneWinsAndHistoryAgrees()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var serviceId = await api.AddServiceAsync();
            var (client, _) = await api.CreateSignedInClientAsync();
            var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
            var (_, staffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
            await api.LinkStaffToServiceAsync(staffId, serviceId);
            var booking = await api.BookAsync(client, serviceId);
            var version = (await BookingApiFactory.GetDetailAsync(client, booking.Id)).RowVersion;

            var responses = await RunTogetherAsync(
                () => client.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel", new CancelBookingRequest(null, version)),
                () => admin.PutAsJsonAsync($"/api/v1/admin/bookings/{booking.Id}/confirm", new ConfirmBookingRequest(staffId, version)));

            Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], responses.Select(r => r.StatusCode).Order());
            var cancelWon = responses[0].StatusCode == HttpStatusCode.OK;
            var detail = await BookingApiFactory.GetDetailAsync(client, booking.Id);
            Assert.Equal(cancelWon ? BookingStatus.Cancelled : BookingStatus.Confirmed, detail.Status);
            Assert.Equal(2, detail.History.Count);
            Assert.Equal(detail.Status, detail.History[^1].ToStatus);
        }
    }

    /// <summary>A Confirmed booking for a new client, with its assigned staff member and an admin ready to act.</summary>
    private async Task<(HttpClient Client, Guid ClientId, BookingCreatedDto Booking, Guid StaffId, HttpClient Admin)> ConfirmedBookingAsync()
    {
        var (client, clientId) = await api.CreateSignedInClientAsync();
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (_, staffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        var serviceId = await api.AddServiceAsync();
        await api.LinkStaffToServiceAsync(staffId, serviceId);
        var booking = await api.BookAsync(client, serviceId);
        var requested = await BookingApiFactory.GetDetailAsync(client, booking.Id);
        var confirm = await admin.PutAsJsonAsync($"/api/v1/admin/bookings/{booking.Id}/confirm", new ConfirmBookingRequest(staffId, requested.RowVersion));
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        return (client, clientId, booking, staffId, admin);
    }

    /// <summary>The user's notifications about one booking, newest first.</summary>
    private static async Task<List<NotificationDto>> NoticesAsync(HttpClient user, int bookingId) =>
        (await user.GetFromJsonAsync<List<NotificationDto>>("/api/v1/notifications/mine"))!
            .Where(n => n.BookingId == bookingId).ToList();

    /// <summary>Starts every request at the same moment and waits for all of them.</summary>
    private static async Task<HttpResponseMessage[]> RunTogetherAsync(params Func<Task<HttpResponseMessage>>[] requests)
    {
        var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sends = requests.Select(async send =>
        {
            await go.Task;
            return await send();
        }).ToList();
        go.SetResult();
        return await Task.WhenAll(sends);
    }

    private async Task<int> ActiveBookingsInSlotAsync(int slotId)
    {
        await using var scope = api.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ExsensicDbContext>();
        return await db.Bookings.CountAsync(b =>
            b.TimeSlotId == slotId && (b.Status == BookingStatus.Requested || b.Status == BookingStatus.Confirmed));
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}
