using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Enums;
using Exsensic.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Exsensic.IntegrationTests.Bookings;

/// <summary>
/// Proves double-booking is impossible under load (FR: one booking per slot). Twenty different clients
/// try to book the same free slot at the same moment through the real API; exactly one must win.
/// This exercises the last line of defence, the filtered unique index UX_Bookings_ActiveSlot, because
/// with requests this close together several of them pass the "is it free?" check before any has saved.
/// </summary>
public sealed class ConcurrentBookingTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    private const int Clients = 20;

    /// <summary>
    /// 20 parallel POST /bookings for one slot: one 201 Created, nineteen 409 slot_unavailable, and one
    /// active booking for the slot in the database.
    /// </summary>
    [Fact]
    public async Task PostBookings_TwentyClientsSameSlot_ExactlyOneSucceeds()
    {
        var serviceId = await api.AddServiceAsync();
        var slotId = await api.AddFutureSlotAsync();
        var clients = new List<HttpClient>();
        for (var i = 0; i < Clients; i++)
        {
            clients.Add((await api.CreateSignedInClientAsync()).Client);
        }

        var request = new CreateBookingRequest(serviceId, slotId, new Dictionary<string, string>
        {
            ["shootType"] = "Product",
            ["location"] = "Studio",
            ["quantity"] = "10",
            ["description"] = "Concurrency test booking.",
        });

        // Every request waits on the same signal, so they are released together.
        var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sends = clients.Select(async client =>
        {
            await go.Task;
            return await client.PostAsJsonAsync("/api/v1/bookings", request);
        }).ToList();
        go.SetResult();
        var responses = await Task.WhenAll(sends);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        var conflicts = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();
        Assert.Equal(Clients - 1, conflicts.Count);
        foreach (var conflict in conflicts)
        {
            using var body = JsonDocument.Parse(await conflict.Content.ReadAsStringAsync());
            Assert.Equal("slot_unavailable", body.RootElement.GetProperty("code").GetString());
        }

        await using var scope = api.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ExsensicDbContext>();
        Assert.Equal(1, await db.Bookings.CountAsync(b =>
            b.TimeSlotId == slotId && (b.Status == BookingStatus.Requested || b.Status == BookingStatus.Confirmed)));
    }

    /// <summary>The winning response is a proper 201: Location header and the booking's reference.</summary>
    [Fact]
    public async Task PostBookings_FreeSlot_Returns201WithLocationAndReference()
    {
        var serviceId = await api.AddServiceAsync();
        var slotId = await api.AddFutureSlotAsync();
        var (client, _) = await api.CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(serviceId, slotId,
            new Dictionary<string, string>
            {
                ["shootType"] = "Team",
                ["location"] = "On location",
                ["quantity"] = "12",
                ["description"] = "Team headshots for the website.",
            }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<BookingCreatedDto>();
        Assert.NotNull(created);
        Assert.Equal(BookingStatus.Requested, created.Status);
        Assert.Equal($"/api/v1/bookings/{created.Id}", response.Headers.Location?.ToString());
    }
}
