using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Enums;
using Exsensic.Contracts.Staff;
using Exsensic.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Exsensic.IntegrationTests.Bookings;

/// <summary>
/// The staff schedule and completion through the real API: staff see only their own Confirmed and
/// Completed work with a requirements summary, and only the assigned staff member or an admin can mark a
/// booking complete, and only once its slot has started.
/// </summary>
public sealed class StaffBookingApiTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    /// <summary>
    /// The schedule holds the caller's Confirmed bookings in time order, with company and summary, and leaves
    /// out other staff's bookings and unconfirmed ones.
    /// </summary>
    [Fact]
    public async Task Schedule_MixedBookings_ReturnsOnlyOwnConfirmedInTimeOrder()
    {
        var (staff, staffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        var (_, otherStaffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        var (client, clientId) = await api.CreateSignedInClientAsync();
        var date = api.NextFutureDate();

        var afternoon = await api.AddBookingInDatabaseAsync(clientId, date, new TimeOnly(14, 0), staffId);
        var morning = await api.AddBookingInDatabaseAsync(clientId, date, new TimeOnly(9, 0), staffId, new Dictionary<string, string>
        {
            ["shootType"] = "Product",
            ["location"] = "Studio",
            ["quantity"] = "10",
            ["description"] = "Shots for the online store.",
            ["notes"] = "Parking at the back.",
        });
        await api.AddBookingInDatabaseAsync(clientId, date, new TimeOnly(11, 0), otherStaffId);
        await api.BookAsync(client, slotId: await api.AddSlotAsync(date, new TimeOnly(16, 30)));

        var schedule = await staff.GetFromJsonAsync<List<BookingSummaryDto>>($"/api/v1/staff/me/bookings?from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}");

        Assert.NotNull(schedule);
        Assert.Equal([morning, afternoon], schedule.Select(b => b.Id));
        var first = schedule[0];
        Assert.Equal(BookingStatus.Confirmed, first.Status);
        Assert.StartsWith("Test Company", first.ClientCompany);
        Assert.Equal("Test Staff", first.StaffName);
        Assert.Equal("Product · Studio · 10 · Shots for the online store.", first.RequirementsSummary);
    }

    /// <summary>A long brief is cut to a short summary ending in an ellipsis.</summary>
    [Fact]
    public async Task Schedule_LongDescription_SummaryIsShortened()
    {
        var (staff, staffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        var (_, clientId) = await api.CreateSignedInClientAsync();
        var date = api.NextFutureDate();
        await api.AddBookingInDatabaseAsync(clientId, date, new TimeOnly(9, 0), staffId,
            new Dictionary<string, string> { ["description"] = new string('x', 900) });

        var schedule = await staff.GetFromJsonAsync<List<BookingSummaryDto>>($"/api/v1/staff/me/bookings?from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}");

        var summary = Assert.Single(schedule!).RequirementsSummary;
        Assert.Equal(120, summary!.Length);
        Assert.EndsWith("…", summary);
    }

    /// <summary>Without dates the schedule returns this week; bad ranges are 400.</summary>
    [Fact]
    public async Task Schedule_DefaultAndInvalidRanges_Returns200Or400()
    {
        var (staff, _) = await api.CreateSignedInClientAsync(RoleNames.Staff);

        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/v1/staff/me/bookings")).StatusCode);
        await AssertProblemAsync(await staff.GetAsync("/api/v1/staff/me/bookings?from=2026-10-10&to=2026-10-01"), HttpStatusCode.BadRequest, "validation_failed");
        await AssertProblemAsync(await staff.GetAsync("/api/v1/staff/me/bookings?from=2026-10-01&to=2027-01-31"), HttpStatusCode.BadRequest, "validation_failed");
    }

    /// <summary>The schedule is for staff only: clients and admins get 403.</summary>
    [Theory]
    [InlineData(RoleNames.Client)]
    [InlineData(RoleNames.Admin)]
    public async Task Schedule_NonStaff_Returns403(string role)
    {
        var (caller, _) = await api.CreateSignedInClientAsync(role);

        await AssertProblemAsync(await caller.GetAsync("/api/v1/staff/me/bookings"), HttpStatusCode.Forbidden, "forbidden");
    }

    /// <summary>Once the slot has started the assigned staff member can complete it, and the client is told.</summary>
    [Fact]
    public async Task Complete_AssignedStaffAfterStart_CompletesAndNotifiesClient()
    {
        var (staff, staffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        var (_, clientId) = await api.CreateSignedInClientAsync();
        var (date, start) = api.EarlierToday();
        var bookingId = await api.AddBookingInDatabaseAsync(clientId, date, start, staffId);
        var detail = await BookingApiFactory.GetDetailAsync(staff, bookingId);

        var response = await staff.PutAsJsonAsync($"/api/v1/staff/bookings/{bookingId}/complete", new CompleteBookingRequest(detail.RowVersion));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await response.Content.ReadFromJsonAsync<BookingDetailDto>();
        Assert.Equal(BookingStatus.Completed, after!.Status);
        Assert.Equal(BookingStatus.Completed, after.History[^1].ToStatus);

        await using var scope = api.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ExsensicDbContext>();
        Assert.True(await db.Notifications.AnyAsync(n =>
            n.BookingId == bookingId && n.UserId == clientId && n.Type == NotificationType.BookingCompleted));
    }

    /// <summary>Before the slot starts, completing is refused by the State pattern.</summary>
    [Fact]
    public async Task Complete_BeforeSlotStart_Returns409InvalidTransition()
    {
        var (staff, staffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        var (_, clientId) = await api.CreateSignedInClientAsync();
        var bookingId = await api.AddBookingInDatabaseAsync(clientId, api.NextFutureDate(), new TimeOnly(9, 0), staffId);
        var detail = await BookingApiFactory.GetDetailAsync(staff, bookingId);

        var response = await staff.PutAsJsonAsync($"/api/v1/staff/bookings/{bookingId}/complete", new CompleteBookingRequest(detail.RowVersion));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "invalid_transition");
    }

    /// <summary>Another staff member gets 404, the client 403, and an admin may complete it.</summary>
    [Fact]
    public async Task Complete_OtherStaffClientAndAdmin_Return404And403And200()
    {
        var (_, staffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        var (otherStaff, _) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (client, clientId) = await api.CreateSignedInClientAsync();
        var (date, start) = api.EarlierToday();
        var bookingId = await api.AddBookingInDatabaseAsync(clientId, date, start, staffId);
        var request = new CompleteBookingRequest((await BookingApiFactory.GetDetailAsync(admin, bookingId)).RowVersion);

        await AssertProblemAsync(await otherStaff.PutAsJsonAsync($"/api/v1/staff/bookings/{bookingId}/complete", request), HttpStatusCode.NotFound, "not_found");
        await AssertProblemAsync(await client.PutAsJsonAsync($"/api/v1/staff/bookings/{bookingId}/complete", request), HttpStatusCode.Forbidden, "forbidden");
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/staff/bookings/{bookingId}/complete", request)).StatusCode);
    }

    /// <summary>A stale RowVersion is refused.</summary>
    [Fact]
    public async Task Complete_StaleRowVersion_Returns409ConcurrencyConflict()
    {
        var (staff, staffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (_, clientId) = await api.CreateSignedInClientAsync();
        var (date, start) = api.EarlierToday();
        var bookingId = await api.AddBookingInDatabaseAsync(clientId, date, start, staffId);
        var stale = (await BookingApiFactory.GetDetailAsync(staff, bookingId)).RowVersion;
        await admin.PutAsJsonAsync($"/api/v1/bookings/{bookingId}/cancel", new CancelBookingRequest("Weather.", stale));

        var response = await staff.PutAsJsonAsync($"/api/v1/staff/bookings/{bookingId}/complete", new CompleteBookingRequest(stale));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "concurrency_conflict");
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}
