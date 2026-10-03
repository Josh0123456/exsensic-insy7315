using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exsensic.Contracts.Admin;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Common;
using Exsensic.Contracts.Enums;

namespace Exsensic.IntegrationTests.Bookings;

/// <summary>
/// The admin dashboard and booking list through the real API. Other tests share the database, so counts
/// are checked as changes from a "before" snapshot, and lists are narrowed to a date only this test uses.
/// </summary>
public sealed class AdminOverviewApiTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    /// <summary>Every status has a count, and new and cancelled bookings move the counts as expected.</summary>
    [Fact]
    public async Task Dashboard_NewAndCancelledBookings_UpdatesStatusCounts()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (client, _) = await api.CreateSignedInClientAsync();
        var before = await admin.GetFromJsonAsync<DashboardDto>("/api/v1/admin/dashboard");

        await api.BookAsync(client);
        var cancelled = await api.BookAsync(client);
        var detail = await BookingApiFactory.GetDetailAsync(client, cancelled.Id);
        await client.PutAsJsonAsync($"/api/v1/bookings/{cancelled.Id}/cancel", new CancelBookingRequest(null, detail.RowVersion));

        var after = await admin.GetFromJsonAsync<DashboardDto>("/api/v1/admin/dashboard");

        Assert.Equal(Enum.GetValues<BookingStatus>().Order(), after!.StatusCounts.Keys.Order());
        Assert.Equal(before!.StatusCounts[BookingStatus.Requested] + 1, after.StatusCounts[BookingStatus.Requested]);
        Assert.Equal(before.StatusCounts[BookingStatus.Cancelled] + 1, after.StatusCounts[BookingStatus.Cancelled]);
        Assert.Equal(after.StatusCounts[BookingStatus.Requested], after.RequestedWaitingCount);
    }

    /// <summary>
    /// Today's list holds today's booking; the next-seven-days list holds one three days away but not one
    /// twenty days away or a cancelled one.
    /// </summary>
    [Fact]
    public async Task Dashboard_BookingsOnDifferentDays_SortsIntoTodayAndNextSevenDays()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (client, clientId) = await api.CreateSignedInClientAsync();
        var (today, earlier) = api.EarlierToday();

        var todayBooking = await api.AddBookingInDatabaseAsync(clientId, today, earlier, Guid.NewGuid());
        var soon = await api.BookAsync(client, slotId: await api.AddSlotAsync(today.AddDays(3), new TimeOnly(7, 0).AddMinutes(Random.Shared.Next(0, 600))));
        var later = await api.BookAsync(client, slotId: await api.AddSlotAsync(today.AddDays(20), new TimeOnly(7, 0).AddMinutes(Random.Shared.Next(0, 600))));
        var cancelled = await api.BookAsync(client, slotId: await api.AddSlotAsync(today.AddDays(4), new TimeOnly(7, 0).AddMinutes(Random.Shared.Next(0, 600))));
        var detail = await BookingApiFactory.GetDetailAsync(client, cancelled.Id);
        await client.PutAsJsonAsync($"/api/v1/bookings/{cancelled.Id}/cancel", new CancelBookingRequest(null, detail.RowVersion));

        var dashboard = await admin.GetFromJsonAsync<DashboardDto>("/api/v1/admin/dashboard");

        Assert.Contains(dashboard!.Today, b => b.Id == todayBooking);
        Assert.Contains(dashboard.NextSevenDays, b => b.Id == soon.Id);
        Assert.DoesNotContain(dashboard.NextSevenDays, b => b.Id == later.Id);
        Assert.DoesNotContain(dashboard.NextSevenDays, b => b.Id == cancelled.Id);
        Assert.All(dashboard.Today, b => Assert.Equal(today, b.SlotDate));
    }

    /// <summary>Requested bookings come first, oldest request first; other statuses follow.</summary>
    [Fact]
    public async Task List_OneDate_RequestedFirstOldestFirst()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (client, clientId) = await api.CreateSignedInClientAsync();
        var date = api.NextFutureDate();
        var confirmed = await api.AddBookingInDatabaseAsync(clientId, date, new TimeOnly(8, 0), Guid.NewGuid());
        var first = await api.BookAsync(client, slotId: await api.AddSlotAsync(date, new TimeOnly(13, 0)));
        var second = await api.BookAsync(client, slotId: await api.AddSlotAsync(date, new TimeOnly(10, 0)));

        var page = await admin.GetFromJsonAsync<PagedResult<BookingSummaryDto>>($"/api/v1/admin/bookings?from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}");

        Assert.Equal([first.Id, second.Id, confirmed], page!.Items.Select(b => b.Id));
        Assert.Equal(3, page.TotalCount);
    }

    /// <summary>The status filter and paging work together, and report the totals for the pager.</summary>
    [Fact]
    public async Task List_StatusFilterAndPaging_ReturnsRequestedSecondPage()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (client, clientId) = await api.CreateSignedInClientAsync();
        var date = api.NextFutureDate();
        await api.AddBookingInDatabaseAsync(clientId, date, new TimeOnly(8, 0), Guid.NewGuid());
        await api.BookAsync(client, slotId: await api.AddSlotAsync(date, new TimeOnly(10, 0)));
        var second = await api.BookAsync(client, slotId: await api.AddSlotAsync(date, new TimeOnly(13, 0)));

        var page = await admin.GetFromJsonAsync<PagedResult<BookingSummaryDto>>(
            $"/api/v1/admin/bookings?status=Requested&from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}&page=2&pageSize=1");

        Assert.Equal(2, page!.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal([second.Id], page.Items.Select(b => b.Id));
        Assert.All(page.Items, b => Assert.Equal("Test Client", b.ClientName));
    }

    /// <summary>A range that ends before it starts is 400.</summary>
    [Fact]
    public async Task List_EndBeforeStart_Returns400ValidationFailed()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);

        await AssertProblemAsync(await admin.GetAsync("/api/v1/admin/bookings?from=2026-10-10&to=2026-10-01"), HttpStatusCode.BadRequest, "validation_failed");
    }

    /// <summary>Clients and staff can't see the dashboard or the full booking list.</summary>
    [Theory]
    [InlineData(RoleNames.Client)]
    [InlineData(RoleNames.Staff)]
    public async Task Overview_NonAdmin_Returns403(string role)
    {
        var (caller, _) = await api.CreateSignedInClientAsync(role);

        await AssertProblemAsync(await caller.GetAsync("/api/v1/admin/dashboard"), HttpStatusCode.Forbidden, "forbidden");
        await AssertProblemAsync(await caller.GetAsync("/api/v1/admin/bookings"), HttpStatusCode.Forbidden, "forbidden");
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}
