using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Catalog;
using Exsensic.Contracts.Common;
using Exsensic.IntegrationTests.Bookings;

namespace Exsensic.IntegrationTests.Catalog;

/// <summary>
/// A slot with an active booking can be neither blocked nor deleted (409 slot_has_booking), so a client
/// never holds a booking on a slot the business has withdrawn.
/// </summary>
public sealed class AdminTimeSlotRulesTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    [Fact]
    public async Task BlockSlot_WithActiveBooking_Returns409SlotHasBooking()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (client, _) = await api.CreateSignedInClientAsync();
        var date = api.NextFutureDate();
        var slotId = await api.AddSlotAsync(date, new TimeOnly(9, 0));
        await api.BookAsync(client, slotId: slotId);
        var slot = await GetSlotAsync(admin, slotId, date);

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/timeslots/{slotId}/block",
            new BlockSlotRequest("Studio maintenance", slot.RowVersion));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, ErrorCodes.SlotHasBooking);
        Assert.False((await GetSlotAsync(admin, slotId, date)).IsBlocked);
    }

    [Fact]
    public async Task DeleteSlot_WithActiveBooking_Returns409SlotHasBooking()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (client, _) = await api.CreateSignedInClientAsync();
        var slotId = await api.AddFutureSlotAsync();
        await api.BookAsync(client, slotId: slotId);

        var response = await admin.DeleteAsync($"/api/v1/admin/timeslots/{slotId}");

        await AssertProblemAsync(response, HttpStatusCode.Conflict, ErrorCodes.SlotHasBooking);
    }

    private static async Task<TimeSlotDto> GetSlotAsync(HttpClient admin, int slotId, DateOnly date)
    {
        var slots = await admin.GetFromJsonAsync<TimeSlotDto[]>($"/api/v1/admin/timeslots?from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}");
        return slots!.Single(s => s.Id == slotId);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}
