using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Enums;
using Exsensic.Contracts.Staff;
using Exsensic.IntegrationTests.Bookings;

namespace Exsensic.IntegrationTests.Security;

/// <summary>
/// Broken access control (OWASP A01). Another user's booking is 404, never 403, so the API
/// never reveals that a booking exists. The booking is unchanged when the check fails.
/// </summary>
public sealed class OwnershipTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    [Fact]
    public async Task ClientB_GetClientAsBooking_Returns404()
    {
        var (alice, _) = await api.CreateSignedInClientAsync();
        var (bob, _) = await api.CreateSignedInClientAsync();
        var booking = await api.BookAsync(alice);

        var response = await bob.GetAsync($"/api/v1/bookings/{booking.Id}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task ClientB_RescheduleClientAsBooking_Returns404()
    {
        var (alice, _) = await api.CreateSignedInClientAsync();
        var (bob, _) = await api.CreateSignedInClientAsync();
        var booking = await api.BookAsync(alice);
        var before = await BookingApiFactory.GetDetailAsync(alice, booking.Id);
        var newSlot = await api.AddFutureSlotAsync();

        var response = await bob.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/reschedule",
            new RescheduleBookingRequest(newSlot, before.RowVersion));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task ClientB_CancelClientAsBooking_Returns404()
    {
        var (alice, _) = await api.CreateSignedInClientAsync();
        var (bob, _) = await api.CreateSignedInClientAsync();
        var booking = await api.BookAsync(alice);
        var before = await BookingApiFactory.GetDetailAsync(alice, booking.Id);

        var response = await bob.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel",
            new CancelBookingRequest("Not mine.", before.RowVersion));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task UnassignedStaff_CompleteBooking_Returns404()
    {
        var (_, clientId) = await api.CreateSignedInClientAsync();
        var (_, assignedStaffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        var (otherStaff, _) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        var (date, start) = api.EarlierToday();
        var bookingId = await api.AddBookingInDatabaseAsync(clientId, date, start, assignedStaffId);
        var assignedClient = await api.SignInAsAsync(assignedStaffId, RoleNames.Staff);
        var before = await BookingApiFactory.GetDetailAsync(assignedClient, bookingId);

        var response = await otherStaff.PutAsJsonAsync(
            $"/api/v1/staff/bookings/{bookingId}/complete",
            new CompleteBookingRequest(before.RowVersion));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "not_found");
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}
