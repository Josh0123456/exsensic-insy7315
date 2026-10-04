using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Enums;
using Exsensic.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Exsensic.IntegrationTests.Bookings;

/// <summary>
/// PUT /bookings/{id}/reschedule and /cancel through the real API: the State pattern decides the outcome,
/// the old slot is freed, stale RowVersions are refused, the cut-off only binds clients, and only the
/// owner client or an admin may make the change.
/// </summary>
public sealed class BookingChangeApiTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    /// <summary>A Requested booking moves to the new slot, stays Requested, and its old slot can be booked again.</summary>
    [Fact]
    public async Task Reschedule_Requested_MovesSlotAndFreesOldOne()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var (otherClient, _) = await api.CreateSignedInClientAsync();
        var serviceId = await api.AddServiceAsync();
        var oldSlot = await api.AddFutureSlotAsync();
        var newSlot = await api.AddFutureSlotAsync();
        var booking = await api.BookAsync(client, serviceId, oldSlot);
        var before = await BookingApiFactory.GetDetailAsync(client, booking.Id);

        var response = await client.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/reschedule",
            new RescheduleBookingRequest(newSlot, before.RowVersion));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await response.Content.ReadFromJsonAsync<BookingDetailDto>();
        Assert.Equal(BookingStatus.Requested, after!.Status);
        Assert.Equal(newSlot, after.TimeSlotId);
        Assert.NotEqual(before.RowVersion, after.RowVersion);
        Assert.Equal(2, after.History.Count);

        // The old slot is free again for someone else.
        var rebooked = await api.BookAsync(otherClient, serviceId, oldSlot);
        Assert.Equal(BookingStatus.Requested, rebooked.Status);
    }

    /// <summary>A Confirmed booking goes back to Requested for re-approval and keeps its proposed staff member.</summary>
    [Fact]
    public async Task Reschedule_Confirmed_ReturnsToRequestedAndKeepsStaff()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var (_, staffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        var booking = await api.BookAsync(client);
        await ConfirmInDatabaseAsync(booking.Id, staffId);
        var before = await BookingApiFactory.GetDetailAsync(client, booking.Id);

        var response = await client.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/reschedule",
            new RescheduleBookingRequest(await api.AddFutureSlotAsync(), before.RowVersion));

        var after = await response.Content.ReadFromJsonAsync<BookingDetailDto>();
        Assert.Equal(BookingStatus.Requested, after!.Status);
        Assert.Equal(staffId, after.StaffUserId);
        Assert.Equal(BookingStatus.Confirmed, after.History[^1].FromStatus);
        Assert.Equal(BookingStatus.Requested, after.History[^1].ToStatus);
    }

    /// <summary>A RowVersion from before someone else's change is refused, and nothing is changed.</summary>
    [Fact]
    public async Task Reschedule_StaleRowVersion_Returns409ConcurrencyConflict()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var booking = await api.BookAsync(client);
        var stale = (await BookingApiFactory.GetDetailAsync(client, booking.Id)).RowVersion;
        await ConfirmInDatabaseAsync(booking.Id, Guid.NewGuid());

        var response = await client.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/reschedule",
            new RescheduleBookingRequest(await api.AddFutureSlotAsync(), stale));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "concurrency_conflict");
        Assert.Equal(BookingStatus.Confirmed, (await BookingApiFactory.GetDetailAsync(client, booking.Id)).Status);
    }

    /// <summary>A RowVersion that isn't valid base64 is a 400 on that field, not a server error.</summary>
    [Fact]
    public async Task Reschedule_MalformedRowVersion_Returns400ValidationFailed()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var booking = await api.BookAsync(client);

        var response = await client.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/reschedule",
            new RescheduleBookingRequest(await api.AddFutureSlotAsync(), "not base64!"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
    }

    /// <summary>Moving to a slot someone else holds is refused, and the booking stays where it was.</summary>
    [Fact]
    public async Task Reschedule_ToTakenSlot_Returns409SlotUnavailable()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var (otherClient, _) = await api.CreateSignedInClientAsync();
        var serviceId = await api.AddServiceAsync();
        var takenSlot = await api.AddFutureSlotAsync();
        await api.BookAsync(otherClient, serviceId, takenSlot);
        var booking = await api.BookAsync(client, serviceId);
        var detail = await BookingApiFactory.GetDetailAsync(client, booking.Id);

        var response = await client.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/reschedule",
            new RescheduleBookingRequest(takenSlot, detail.RowVersion));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "slot_unavailable");
        Assert.Equal(detail.TimeSlotId, (await BookingApiFactory.GetDetailAsync(client, booking.Id)).TimeSlotId);
    }

    /// <summary>The assigned staff member can view but not move a booking (403); another client gets 404.</summary>
    [Fact]
    public async Task Reschedule_StaffOrOtherClient_Returns403Or404()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var (staff, staffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        var (stranger, _) = await api.CreateSignedInClientAsync();
        var booking = await api.BookAsync(client);
        await ConfirmInDatabaseAsync(booking.Id, staffId);
        var detail = await BookingApiFactory.GetDetailAsync(client, booking.Id);
        var request = new RescheduleBookingRequest(await api.AddFutureSlotAsync(), detail.RowVersion);

        await AssertProblemAsync(await staff.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/reschedule", request), HttpStatusCode.Forbidden, "forbidden");
        await AssertProblemAsync(await stranger.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/reschedule", request), HttpStatusCode.NotFound, "not_found");
    }

    /// <summary>A client can cancel their Requested booking; the reason is kept and the timeline grows.</summary>
    [Fact]
    public async Task Cancel_RequestedByClient_CancelsWithReason()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var booking = await api.BookAsync(client);
        var detail = await BookingApiFactory.GetDetailAsync(client, booking.Id);

        var response = await client.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel",
            new CancelBookingRequest("Plans changed.", detail.RowVersion));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await response.Content.ReadFromJsonAsync<BookingDetailDto>();
        Assert.Equal(BookingStatus.Cancelled, after!.Status);
        Assert.Equal("Plans changed.", after.CancellationReason);
        Assert.Equal(BookingStatus.Cancelled, after.History[^1].ToStatus);
    }

    /// <summary>Inside the 24-hour cut-off a client can't cancel a confirmed booking, but an admin can.</summary>
    [Fact]
    public async Task Cancel_ConfirmedInsideCutoff_ClientRefusedAdminAllowed()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var booking = await api.BookAsync(client, slotId: await api.AddSlotStartingSoonAsync());
        await ConfirmInDatabaseAsync(booking.Id, Guid.NewGuid());
        var detail = await BookingApiFactory.GetDetailAsync(client, booking.Id);
        var request = new CancelBookingRequest(null, detail.RowVersion);

        var clientResponse = await client.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel", request);
        await AssertProblemAsync(clientResponse, HttpStatusCode.Conflict, "cancel_window_closed");

        var adminResponse = await admin.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel", request);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        Assert.Equal(BookingStatus.Cancelled, (await adminResponse.Content.ReadFromJsonAsync<BookingDetailDto>())!.Status);
    }

    /// <summary>Outside the cut-off a client can cancel a confirmed booking.</summary>
    [Fact]
    public async Task Cancel_ConfirmedOutsideCutoffByClient_Cancels()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var booking = await api.BookAsync(client);
        await ConfirmInDatabaseAsync(booking.Id, Guid.NewGuid());
        var detail = await BookingApiFactory.GetDetailAsync(client, booking.Id);

        var response = await client.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel", new CancelBookingRequest(null, detail.RowVersion));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>A cancelled booking can't be cancelled again: the State pattern refuses it.</summary>
    [Fact]
    public async Task Cancel_AlreadyCancelled_Returns409InvalidTransition()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var booking = await api.BookAsync(client);
        var first = await BookingApiFactory.GetDetailAsync(client, booking.Id);
        var cancelled = await (await client.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel",
            new CancelBookingRequest(null, first.RowVersion))).Content.ReadFromJsonAsync<BookingDetailDto>();

        var response = await client.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel", new CancelBookingRequest(null, cancelled!.RowVersion));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "invalid_transition");
    }

    /// <summary>Confirms a booking through the domain method, standing in for the admin endpoint.</summary>
    private async Task ConfirmInDatabaseAsync(int bookingId, Guid staffId)
    {
        await using var scope = api.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ExsensicDbContext>();
        var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId);
        booking.Confirm(staffId, Guid.NewGuid(), DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}
