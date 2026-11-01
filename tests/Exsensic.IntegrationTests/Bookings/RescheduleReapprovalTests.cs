using System.Net;
using System.Net.Http.Json;
using Exsensic.Contracts.Admin;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Enums;

namespace Exsensic.IntegrationTests.Bookings;

/// <summary>
/// After a Confirmed booking is rescheduled it is Requested again and keeps its staff member as a proposal.
/// That proposal must not count as the staff member being busy, or the admin could never re-approve the
/// booking with them from the staff list (found while checking the whole journey in the Web pages).
/// </summary>
public sealed class RescheduleReapprovalTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    /// <summary>
    /// The only qualified staff member is offered again for the rescheduled booking, and the admin can
    /// re-confirm with them.
    /// </summary>
    [Fact]
    public async Task StaffOptions_RescheduledBooking_StillOffersItsProposedStaffAndCanReconfirm()
    {
        var (client, admin, staffId, serviceId) = await SetUpAsync();
        var booking = await api.BookAsync(client, serviceId);
        await ConfirmAsync(admin, client, booking.Id, staffId);
        var newSlot = await api.AddFutureSlotAsync();
        var moved = await Reschedule(client, booking.Id, newSlot);
        Assert.Equal(BookingStatus.Requested, moved.Status);
        Assert.Equal(staffId, moved.StaffUserId);

        var options = await admin.GetFromJsonAsync<List<StaffOptionDto>>($"/api/v1/admin/staff?serviceId={serviceId}&timeSlotId={newSlot}");

        Assert.Contains(options!, o => o.UserId == staffId);
        var again = await admin.PutAsJsonAsync($"/api/v1/admin/bookings/{booking.Id}/confirm", new ConfirmBookingRequest(staffId, moved.RowVersion));
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    /// <summary>
    /// A Requested booking that merely carries a proposed staff member doesn't stop that person being offered
    /// for another booking at the same time. Only a Confirmed booking commits them.
    /// </summary>
    [Fact]
    public async Task StaffOptions_OtherBookingOnlyProposesTheStaff_StaffIsStillOffered()
    {
        var (client, admin, staffId, serviceId) = await SetUpAsync();
        var date = api.NextFutureDate();
        var first = await api.BookAsync(client, serviceId, await api.AddSlotAsync(date, new TimeOnly(9, 0)));
        await ConfirmAsync(admin, client, first.Id, staffId);
        await Reschedule(client, first.Id, await api.AddSlotAsync(api.NextFutureDate(), new TimeOnly(9, 0)));
        var movedTo = (await BookingApiFactory.GetDetailAsync(client, first.Id)).TimeSlotId;
        var otherDay = (await BookingApiFactory.GetDetailAsync(client, first.Id)).SlotDate;
        var overlapping = await api.AddSlotAsync(otherDay, new TimeOnly(10, 0));

        var options = await admin.GetFromJsonAsync<List<StaffOptionDto>>($"/api/v1/admin/staff?serviceId={serviceId}&timeSlotId={overlapping}");

        Assert.NotEqual(0, movedTo);
        Assert.Contains(options!, o => o.UserId == staffId);
    }

    /// <summary>A Confirmed booking at an overlapping time still makes the staff member unavailable (no double assignment).</summary>
    [Fact]
    public async Task StaffOptions_OtherBookingConfirmedAtOverlappingTime_StaffIsNotOffered()
    {
        var (client, admin, staffId, serviceId) = await SetUpAsync();
        var date = api.NextFutureDate();
        var first = await api.BookAsync(client, serviceId, await api.AddSlotAsync(date, new TimeOnly(9, 0)));
        await ConfirmAsync(admin, client, first.Id, staffId);
        var overlapping = await api.AddSlotAsync(date, new TimeOnly(10, 0));

        var options = await admin.GetFromJsonAsync<List<StaffOptionDto>>($"/api/v1/admin/staff?serviceId={serviceId}&timeSlotId={overlapping}");

        Assert.DoesNotContain(options!, o => o.UserId == staffId);
    }

    private async Task<(HttpClient Client, HttpClient Admin, Guid StaffId, int ServiceId)> SetUpAsync()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (_, staffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        var serviceId = await api.AddServiceAsync();
        await api.LinkStaffToServiceAsync(staffId, serviceId);
        return (client, admin, staffId, serviceId);
    }

    private static async Task ConfirmAsync(HttpClient admin, HttpClient client, int bookingId, Guid staffId)
    {
        var version = (await BookingApiFactory.GetDetailAsync(client, bookingId)).RowVersion;
        var response = await admin.PutAsJsonAsync($"/api/v1/admin/bookings/{bookingId}/confirm", new ConfirmBookingRequest(staffId, version));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<BookingDetailDto> Reschedule(HttpClient client, int bookingId, int newSlotId)
    {
        var version = (await BookingApiFactory.GetDetailAsync(client, bookingId)).RowVersion;
        var response = await client.PutAsJsonAsync($"/api/v1/bookings/{bookingId}/reschedule", new RescheduleBookingRequest(newSlotId, version));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookingDetailDto>())!;
    }
}
