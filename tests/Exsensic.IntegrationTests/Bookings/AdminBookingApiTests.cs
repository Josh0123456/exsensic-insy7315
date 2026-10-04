using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exsensic.Contracts.Admin;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Enums;
using Exsensic.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Exsensic.IntegrationTests.Bookings;

/// <summary>
/// The admin's review through the real API: staff options list only qualified, active, free staff;
/// confirm re-checks the staff member and notifies the client and staff; reject stores the prefixed
/// reason; and only admins may use these endpoints.
/// </summary>
public sealed class AdminBookingApiTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    /// <summary>
    /// Of four staff linked or not to the service, only the qualified, active one without an overlapping
    /// booking is offered.
    /// </summary>
    [Fact]
    public async Task StaffOptions_MixedStaff_ListsOnlyQualifiedActiveFreeStaff()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (client, _) = await api.CreateSignedInClientAsync();
        var serviceId = await api.AddServiceAsync();
        var date = api.NextFutureDate();
        var slotId = await api.AddSlotAsync(date, new TimeOnly(9, 0));
        var overlappingSlotId = await api.AddSlotAsync(date, new TimeOnly(10, 0));

        var free = await NewLinkedStaffAsync(serviceId);
        var busy = await NewLinkedStaffAsync(serviceId);
        var inactive = await NewLinkedStaffAsync(serviceId);
        var (_, unqualified) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        await api.DeactivateAsync(inactive);

        // The busy staff member already holds a confirmed booking from 10:00 to 12:00 that day.
        var other = await api.BookAsync(client, serviceId, overlappingSlotId);
        await ConfirmAsync(admin, other.Id, busy, client);

        var options = await admin.GetFromJsonAsync<List<StaffOptionDto>>($"/api/v1/admin/staff?serviceId={serviceId}&timeSlotId={slotId}");

        var option = Assert.Single(options!);
        Assert.Equal(free, option.UserId);
        Assert.Equal("Test Staff", option.FullName);
        Assert.Equal("Photographer", option.JobTitle);
        Assert.DoesNotContain(options!, o => o.UserId == unqualified);
    }

    /// <summary>Missing ids are 400; a slot that doesn't exist is 404.</summary>
    [Fact]
    public async Task StaffOptions_MissingOrUnknownIds_Returns400Or404()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var serviceId = await api.AddServiceAsync();

        await AssertProblemAsync(await admin.GetAsync("/api/v1/admin/staff"), HttpStatusCode.BadRequest, "validation_failed");
        await AssertProblemAsync(await admin.GetAsync($"/api/v1/admin/staff?serviceId={serviceId}&timeSlotId=999999"), HttpStatusCode.NotFound, "not_found");
    }

    /// <summary>Confirming assigns the staff member and notifies both the client and the staff member.</summary>
    [Fact]
    public async Task Confirm_QualifiedFreeStaff_ConfirmsAndNotifies()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (client, clientId) = await api.CreateSignedInClientAsync();
        var serviceId = await api.AddServiceAsync();
        var staffId = await NewLinkedStaffAsync(serviceId);
        var booking = await api.BookAsync(client, serviceId);

        var detail = await ConfirmAsync(admin, booking.Id, staffId, client);

        Assert.Equal(BookingStatus.Confirmed, detail.Status);
        Assert.Equal(staffId, detail.StaffUserId);
        Assert.Equal("Test Staff", detail.StaffName);
        Assert.Equal("Test Admin", detail.History[^1].ChangedByName);

        await using var scope = api.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ExsensicDbContext>();
        var notifications = await db.Notifications.Where(n => n.BookingId == booking.Id).ToListAsync();
        Assert.Contains(notifications, n => n.UserId == clientId && n.Type == NotificationType.BookingConfirmed);
        Assert.Contains(notifications, n => n.UserId == staffId && n.Type == NotificationType.StaffAssigned);
    }

    /// <summary>A staff member who doesn't deliver the service is refused, and the booking stays Requested.</summary>
    [Fact]
    public async Task Confirm_UnqualifiedStaff_Returns409StaffUnavailable()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (client, _) = await api.CreateSignedInClientAsync();
        var (_, unqualified) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        var booking = await api.BookAsync(client);
        var detail = await BookingApiFactory.GetDetailAsync(client, booking.Id);

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/bookings/{booking.Id}/confirm",
            new ConfirmBookingRequest(unqualified, detail.RowVersion));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "staff_unavailable");
        Assert.Equal(BookingStatus.Requested, (await BookingApiFactory.GetDetailAsync(client, booking.Id)).Status);
    }

    /// <summary>A staff member with an overlapping booking that day is refused.</summary>
    [Fact]
    public async Task Confirm_StaffBookedAtSameTime_Returns409StaffUnavailable()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (client, _) = await api.CreateSignedInClientAsync();
        var serviceId = await api.AddServiceAsync();
        var staffId = await NewLinkedStaffAsync(serviceId);
        var date = api.NextFutureDate();
        var first = await api.BookAsync(client, serviceId, await api.AddSlotAsync(date, new TimeOnly(9, 0)));
        var second = await api.BookAsync(client, serviceId, await api.AddSlotAsync(date, new TimeOnly(10, 30)));
        await ConfirmAsync(admin, first.Id, staffId, client);
        var detail = await BookingApiFactory.GetDetailAsync(client, second.Id);

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/bookings/{second.Id}/confirm",
            new ConfirmBookingRequest(staffId, detail.RowVersion));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "staff_unavailable");
    }

    /// <summary>A booking that is already confirmed can't be confirmed again.</summary>
    [Fact]
    public async Task Confirm_AlreadyConfirmed_Returns409InvalidTransition()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (client, _) = await api.CreateSignedInClientAsync();
        var serviceId = await api.AddServiceAsync();
        var staffId = await NewLinkedStaffAsync(serviceId);
        var booking = await api.BookAsync(client, serviceId);
        var confirmed = await ConfirmAsync(admin, booking.Id, staffId, client);

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/bookings/{booking.Id}/confirm",
            new ConfirmBookingRequest(staffId, confirmed.RowVersion));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "invalid_transition");
    }

    /// <summary>An admin working from an out-of-date page is refused instead of overwriting the client's change.</summary>
    [Fact]
    public async Task Confirm_StaleRowVersion_Returns409ConcurrencyConflict()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (client, _) = await api.CreateSignedInClientAsync();
        var serviceId = await api.AddServiceAsync();
        var staffId = await NewLinkedStaffAsync(serviceId);
        var booking = await api.BookAsync(client, serviceId);
        var stale = (await BookingApiFactory.GetDetailAsync(client, booking.Id)).RowVersion;
        await client.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/reschedule",
            new RescheduleBookingRequest(await api.AddFutureSlotAsync(), stale));

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/bookings/{booking.Id}/confirm",
            new ConfirmBookingRequest(staffId, stale));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "concurrency_conflict");
    }

    /// <summary>Rejecting cancels the booking and stores the reason with the "Rejected: " prefix.</summary>
    [Fact]
    public async Task Reject_WithReason_CancelsWithPrefixedReason()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (client, _) = await api.CreateSignedInClientAsync();
        var booking = await api.BookAsync(client);
        var detail = await BookingApiFactory.GetDetailAsync(client, booking.Id);

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/bookings/{booking.Id}/reject",
            new RejectBookingRequest("No photographer is free that week.", detail.RowVersion));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await response.Content.ReadFromJsonAsync<BookingDetailDto>();
        Assert.Equal(BookingStatus.Cancelled, after!.Status);
        Assert.Equal("Rejected: No photographer is free that week.", after.CancellationReason);
    }

    /// <summary>A rejection without a reason is a 400 validation error.</summary>
    [Fact]
    public async Task Reject_EmptyReason_Returns400ValidationFailed()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (client, _) = await api.CreateSignedInClientAsync();
        var booking = await api.BookAsync(client);
        var detail = await BookingApiFactory.GetDetailAsync(client, booking.Id);

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/bookings/{booking.Id}/reject",
            new RejectBookingRequest("", detail.RowVersion));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
    }

    /// <summary>Clients and staff can't use the admin endpoints (403 forbidden).</summary>
    [Theory]
    [InlineData(RoleNames.Client)]
    [InlineData(RoleNames.Staff)]
    public async Task AdminEndpoints_NonAdmin_Returns403(string role)
    {
        var (caller, _) = await api.CreateSignedInClientAsync(role);
        var (client, _) = await api.CreateSignedInClientAsync();
        var booking = await api.BookAsync(client);
        var detail = await BookingApiFactory.GetDetailAsync(client, booking.Id);

        await AssertProblemAsync(await caller.GetAsync("/api/v1/admin/staff?serviceId=1&timeSlotId=1"), HttpStatusCode.Forbidden, "forbidden");
        await AssertProblemAsync(await caller.PutAsJsonAsync($"/api/v1/admin/bookings/{booking.Id}/reject",
            new RejectBookingRequest("Trying it on.", detail.RowVersion)), HttpStatusCode.Forbidden, "forbidden");
    }

    /// <summary>Creates a staff member who delivers the service and returns their id.</summary>
    private async Task<Guid> NewLinkedStaffAsync(int serviceId)
    {
        var (_, staffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        await api.LinkStaffToServiceAsync(staffId, serviceId);
        return staffId;
    }

    /// <summary>Confirms a booking through the admin endpoint and returns the updated booking.</summary>
    private static async Task<BookingDetailDto> ConfirmAsync(HttpClient admin, int bookingId, Guid staffId, HttpClient owner)
    {
        var detail = await BookingApiFactory.GetDetailAsync(owner, bookingId);
        var response = await admin.PutAsJsonAsync($"/api/v1/admin/bookings/{bookingId}/confirm",
            new ConfirmBookingRequest(staffId, detail.RowVersion));
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
