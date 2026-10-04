using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Common;
using Exsensic.Contracts.Enums;
using Exsensic.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Exsensic.IntegrationTests.Bookings;

/// <summary>
/// GET /bookings/mine and GET /bookings/{id} through the real API: clients only see their own bookings,
/// lists are filtered, ordered and paged, and anyone without access to a booking gets 404, never 403.
/// </summary>
public sealed class BookingReadApiTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    private static readonly Dictionary<string, string> Answers = new()
    {
        ["shootType"] = "Product",
        ["location"] = "Studio",
        ["quantity"] = "10",
        ["description"] = "Shots for the online store.",
    };

    /// <summary>A client's list holds only their own bookings, newest slot first, with names filled in.</summary>
    [Fact]
    public async Task Mine_TwoClients_ReturnsOnlyCallersBookingsNewestSlotFirst()
    {
        var (alice, _) = await api.CreateSignedInClientAsync();
        var (bob, _) = await api.CreateSignedInClientAsync();
        var earlier = await BookAsync(alice);
        var later = await BookAsync(alice);
        await BookAsync(bob);

        var page = await alice.GetFromJsonAsync<PagedResult<BookingSummaryDto>>("/api/v1/bookings/mine");

        Assert.NotNull(page);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal([later.Id, earlier.Id], page.Items.Select(b => b.Id));
        Assert.All(page.Items, b => Assert.Equal("Test Client", b.ClientName));
        Assert.All(page.Items, b => Assert.StartsWith("Test Company", b.ClientCompany));
        Assert.All(page.Items, b => Assert.Null(b.StaffName));
    }

    /// <summary>The status filter only returns bookings in that status.</summary>
    [Fact]
    public async Task Mine_StatusFilter_ReturnsOnlyThatStatus()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var kept = await BookAsync(client);
        var cancelled = await BookAsync(client);
        await CancelInDatabaseAsync(cancelled.Id);

        var requested = await client.GetFromJsonAsync<PagedResult<BookingSummaryDto>>("/api/v1/bookings/mine?status=Requested");
        var cancelledPage = await client.GetFromJsonAsync<PagedResult<BookingSummaryDto>>("/api/v1/bookings/mine?status=Cancelled");

        Assert.Equal([kept.Id], requested!.Items.Select(b => b.Id));
        Assert.Equal([cancelled.Id], cancelledPage!.Items.Select(b => b.Id));
    }

    /// <summary>Paging splits the list and reports the totals the pager needs.</summary>
    [Fact]
    public async Task Mine_SecondPage_ReturnsRemainingItemAndTotals()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var oldest = await BookAsync(client);
        await BookAsync(client);
        await BookAsync(client);

        var page = await client.GetFromJsonAsync<PagedResult<BookingSummaryDto>>("/api/v1/bookings/mine?page=2&pageSize=2");

        Assert.NotNull(page);
        Assert.Equal(2, page.Page);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal([oldest.Id], page.Items.Select(b => b.Id));
    }

    /// <summary>A status that isn't one of the four contract values is rejected, not ignored.</summary>
    [Fact]
    public async Task Mine_UnknownStatus_Returns400ValidationFailed()
    {
        var (client, _) = await api.CreateSignedInClientAsync();

        var response = await client.GetAsync("/api/v1/bookings/mine?status=Pending");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
    }

    /// <summary>"My bookings" is for clients; staff get 403 forbidden (wrong role), anonymous callers 401.</summary>
    [Fact]
    public async Task Mine_StaffOrAnonymous_Returns403Or401()
    {
        var (staff, _) = await api.CreateSignedInClientAsync(RoleNames.Staff);

        await AssertProblemAsync(await staff.GetAsync("/api/v1/bookings/mine"), HttpStatusCode.Forbidden, "forbidden");
        await AssertProblemAsync(await api.CreateAnonymousClient().GetAsync("/api/v1/bookings/mine"), HttpStatusCode.Unauthorized, "unauthenticated");
    }

    /// <summary>The owner sees the full details: requirements, contact details and the status timeline with names.</summary>
    [Fact]
    public async Task Get_Owner_ReturnsFullDetails()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var created = await BookAsync(client);

        var detail = await client.GetFromJsonAsync<BookingDetailDto>($"/api/v1/bookings/{created.Id}");

        Assert.NotNull(detail);
        Assert.Equal(created.Reference, detail.Reference);
        Assert.Equal(BookingStatus.Requested, detail.Status);
        Assert.Equal("Test Client", detail.ClientName);
        Assert.EndsWith("@example.com", detail.ClientEmail);
        Assert.Equal("0100000000", detail.ClientPhone);
        Assert.Equal("Shots for the online store.", detail.Requirements["description"]);
        var history = Assert.Single(detail.History);
        Assert.Null(history.FromStatus);
        Assert.Equal(BookingStatus.Requested, history.ToStatus);
        Assert.Equal("Test Client", history.ChangedByName);
        Assert.False(string.IsNullOrEmpty(detail.RowVersion));
    }

    /// <summary>Another client gets 404, exactly like a booking that doesn't exist.</summary>
    [Fact]
    public async Task Get_OtherClient_Returns404LikeMissingBooking()
    {
        var (owner, _) = await api.CreateSignedInClientAsync();
        var (stranger, _) = await api.CreateSignedInClientAsync();
        var created = await BookAsync(owner);

        await AssertProblemAsync(await stranger.GetAsync($"/api/v1/bookings/{created.Id}"), HttpStatusCode.NotFound, "not_found");
        await AssertProblemAsync(await stranger.GetAsync("/api/v1/bookings/999999"), HttpStatusCode.NotFound, "not_found");
    }

    /// <summary>Admins can open any booking.</summary>
    [Fact]
    public async Task Get_Admin_Returns200()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var created = await BookAsync(client);

        var response = await admin.GetAsync($"/api/v1/bookings/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>The assigned staff member can open the booking and is named on it; other staff get 404.</summary>
    [Fact]
    public async Task Get_AssignedStaffOnly_Returns200ElseOtherStaff404()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var (assigned, assignedId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        var (otherStaff, _) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        var created = await BookAsync(client);
        await ConfirmInDatabaseAsync(created.Id, assignedId);

        var detail = await assigned.GetFromJsonAsync<BookingDetailDto>($"/api/v1/bookings/{created.Id}");

        Assert.NotNull(detail);
        Assert.Equal(BookingStatus.Confirmed, detail.Status);
        Assert.Equal(assignedId, detail.StaffUserId);
        Assert.Equal("Test Staff", detail.StaffName);
        await AssertProblemAsync(await otherStaff.GetAsync($"/api/v1/bookings/{created.Id}"), HttpStatusCode.NotFound, "not_found");
    }

    /// <summary>Books a new slot for the client through POST /bookings and returns the result.</summary>
    private async Task<BookingCreatedDto> BookAsync(HttpClient client)
    {
        var serviceId = await api.AddServiceAsync();
        var slotId = await api.AddFutureSlotAsync();
        var response = await client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(serviceId, slotId, Answers));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookingCreatedDto>())!;
    }

    /// <summary>Confirms a booking through the domain method, standing in for the admin endpoint (Step 11).</summary>
    private async Task ConfirmInDatabaseAsync(int bookingId, Guid staffId)
    {
        await using var scope = api.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ExsensicDbContext>();
        var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId);
        booking.Confirm(staffId, Guid.NewGuid(), DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }

    /// <summary>Cancels a booking through the domain method, standing in for the cancel endpoint (Step 10).</summary>
    private async Task CancelInDatabaseAsync(int bookingId)
    {
        await using var scope = api.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ExsensicDbContext>();
        var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId);
        booking.Cancel(null, booking.ClientUserId, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}
