using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exsensic.Contracts.Admin;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Requirements;
using Exsensic.Core.Entities;
using Exsensic.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Exsensic.IntegrationTests.Bookings;

/// <summary>
/// Business rules that a coverage run showed no other test exercised: the public requirement-template route,
/// the edges of the staff options list, confirming with a staff member who can't take the booking, moving a
/// booking to the slot it already has, and the staff schedule's requirements summary.
/// </summary>
public sealed class BookingRuleGapTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    /// <summary>GET /services/{serviceId}/requirement-template needs no sign-in and returns the category's form fields.</summary>
    [Fact]
    public async Task RequirementTemplate_ActiveService_ReturnsFieldsWithoutSignIn()
    {
        var serviceId = await api.AddServiceAsync();

        var response = await api.CreateAnonymousClient().GetAsync($"/api/v1/services/{serviceId}/requirement-template");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var template = (await response.Content.ReadFromJsonAsync<RequirementTemplateDto>())!;
        Assert.Equal(Exsensic.Contracts.Enums.ServiceCategory.Photoshoot, template.Category);
        var keys = template.Fields.Select(f => f.Key).ToList();
        Assert.Contains("shootType", keys);
        Assert.Contains("description", keys);
        Assert.Contains(template.Fields, f => f.Required);
    }

    /// <summary>An unknown service, or one that has been archived, has no template to show: 404 not_found.</summary>
    [Fact]
    public async Task RequirementTemplate_UnknownOrArchivedService_Returns404()
    {
        var anonymous = api.CreateAnonymousClient();
        var archivedId = await api.AddServiceAsync();
        await using (var scope = api.NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ExsensicDbContext>();
            (await db.Services.SingleAsync(s => s.Id == archivedId)).IsActive = false;
            await db.SaveChangesAsync();
        }

        await AssertProblemAsync(await anonymous.GetAsync("/api/v1/services/999999/requirement-template"), HttpStatusCode.NotFound, "not_found");
        await AssertProblemAsync(await anonymous.GetAsync($"/api/v1/services/{archivedId}/requirement-template"), HttpStatusCode.NotFound, "not_found");
    }

    /// <summary>The staff list for an unknown service or an unknown slot is 404 not_found.</summary>
    [Fact]
    public async Task StaffOptions_UnknownServiceOrSlot_Returns404()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var serviceId = await api.AddServiceAsync();
        var slotId = await api.AddFutureSlotAsync();

        await AssertProblemAsync(await admin.GetAsync($"/api/v1/admin/staff?serviceId=999999&timeSlotId={slotId}"), HttpStatusCode.NotFound, "not_found");
        await AssertProblemAsync(await admin.GetAsync($"/api/v1/admin/staff?serviceId={serviceId}&timeSlotId=999999"), HttpStatusCode.NotFound, "not_found");
    }

    /// <summary>When nobody is qualified for the service, the list is empty (200), not an error.</summary>
    [Fact]
    public async Task StaffOptions_NoQualifiedStaff_ReturnsEmptyList()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var serviceId = await api.AddServiceAsync();
        var slotId = await api.AddFutureSlotAsync();
        await api.CreateSignedInClientAsync(RoleNames.Staff);

        var options = await admin.GetFromJsonAsync<List<StaffOptionDto>>($"/api/v1/admin/staff?serviceId={serviceId}&timeSlotId={slotId}");

        Assert.Empty(options!);
    }

    /// <summary>Several qualified, free staff members are listed by name, A to Z, so the admin can find someone.</summary>
    [Fact]
    public async Task StaffOptions_SeveralQualifiedStaff_AreOrderedByName()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var serviceId = await api.AddServiceAsync();
        var slotId = await api.AddFutureSlotAsync();
        var zed = await NewLinkedStaffAsync(serviceId, "Zed Staff");
        var amy = await NewLinkedStaffAsync(serviceId, "Amy Staff");
        var mia = await NewLinkedStaffAsync(serviceId, "mia Staff");

        var options = await admin.GetFromJsonAsync<List<StaffOptionDto>>($"/api/v1/admin/staff?serviceId={serviceId}&timeSlotId={slotId}");

        Assert.Equal([amy, mia, zed], options!.Select(o => o.UserId));
    }

    /// <summary>A staff member whose account has been deactivated can't be assigned: 409 staff_unavailable, booking unchanged.</summary>
    [Fact]
    public async Task Confirm_DeactivatedStaff_Returns409StaffUnavailable()
    {
        var (client, admin, serviceId, booking) = await RequestedBookingAsync();
        var staffId = await NewLinkedStaffAsync(serviceId);
        await api.DeactivateAsync(staffId);
        var version = (await BookingApiFactory.GetDetailAsync(client, booking.Id)).RowVersion;

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/bookings/{booking.Id}/confirm", new ConfirmBookingRequest(staffId, version));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "staff_unavailable");
        Assert.Equal(Exsensic.Contracts.Enums.BookingStatus.Requested, (await BookingApiFactory.GetDetailAsync(client, booking.Id)).Status);
    }

    /// <summary>An empty staff id is bad input: 400 validation_failed naming the field, never a 500. The booking stays Requested.</summary>
    [Fact]
    public async Task Confirm_EmptyStaffId_Returns400ValidationFailedAndChangesNothing()
    {
        var (client, admin, _, booking) = await RequestedBookingAsync();
        var version = (await BookingApiFactory.GetDetailAsync(client, booking.Id)).RowVersion;

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/bookings/{booking.Id}/confirm", new ConfirmBookingRequest(Guid.Empty, version));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("StaffUserId", out _));
        Assert.Equal(Exsensic.Contracts.Enums.BookingStatus.Requested, (await BookingApiFactory.GetDetailAsync(client, booking.Id)).Status);
    }

    /// <summary>Moving a booking to the slot it already has is refused with 409 slot_unavailable.</summary>
    [Fact]
    public async Task Reschedule_ToTheSameSlot_Returns409SlotUnavailable()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var slotId = await api.AddFutureSlotAsync();
        var booking = await api.BookAsync(client, slotId: slotId);
        var version = (await BookingApiFactory.GetDetailAsync(client, booking.Id)).RowVersion;

        var response = await client.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/reschedule", new RescheduleBookingRequest(slotId, version));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "slot_unavailable");
    }

    /// <summary>A booking whose requirement answers are all blank has no summary on the staff schedule (null, not an empty line).</summary>
    [Fact]
    public async Task StaffSchedule_BlankRequirementAnswers_HaveNoSummary()
    {
        var (_, clientId) = await api.CreateSignedInClientAsync();
        var (staff, staffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        var date = api.NextFutureDate();
        await api.AddBookingInDatabaseAsync(clientId, date, new TimeOnly(9, 0), staffId,
            new Dictionary<string, string> { ["description"] = " " });

        var schedule = await staff.GetFromJsonAsync<List<BookingSummaryDto>>($"/api/v1/staff/me/bookings?from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}");

        Assert.Null(Assert.Single(schedule!).RequirementsSummary);
    }

    /// <summary>A new client, an admin, and a Requested booking by that client.</summary>
    private async Task<(HttpClient Client, HttpClient Admin, int ServiceId, BookingCreatedDto Booking)> RequestedBookingAsync()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var serviceId = await api.AddServiceAsync();
        return (client, admin, serviceId, await api.BookAsync(client, serviceId));
    }

    /// <summary>A staff member qualified for the service, with a chosen display name.</summary>
    private async Task<Guid> NewLinkedStaffAsync(int serviceId, string fullName = "Test Staff")
    {
        var (_, staffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        await api.LinkStaffToServiceAsync(staffId, serviceId);

        await using var scope = api.NewScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(staffId.ToString()))!;
        user.FullName = fullName;
        Assert.True((await users.UpdateAsync(user)).Succeeded);
        return staffId;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}
