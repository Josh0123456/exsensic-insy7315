using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Exsensic.Contracts.Bookings;
using Exsensic.IntegrationTests.Bookings;

namespace Exsensic.IntegrationTests.Security;

/// <summary>
/// The API never trusts the form. Unknown keys, oversized text, bad enum values and malformed
/// JSON all fail with 400, never 500.
/// </summary>
public sealed class InputValidationTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    [Fact]
    public async Task CreateBooking_UnknownRequirementKey_Returns400()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var serviceId = await api.AddServiceAsync();
        var slotId = await api.AddFutureSlotAsync();
        var request = new CreateBookingRequest(serviceId, slotId, new Dictionary<string, string>
        {
            ["shootType"] = "Product",
            ["location"] = "Studio",
            ["quantity"] = "10",
            ["description"] = "Shots.",
            ["budget"] = "5000"
        });

        var response = await client.PostAsJsonAsync("/api/v1/bookings", request);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task CreateBooking_OversizedDescription_Returns400()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var serviceId = await api.AddServiceAsync();
        var slotId = await api.AddFutureSlotAsync();
        var request = new CreateBookingRequest(serviceId, slotId, new Dictionary<string, string>
        {
            ["shootType"] = "Product",
            ["location"] = "Studio",
            ["quantity"] = "10",
            ["description"] = new string('a', 1500)
        });

        var response = await client.PostAsJsonAsync("/api/v1/bookings", request);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Availability_RangeOverSixtyDays_Returns400()
    {
        var client = api.CreateAnonymousClient();
        var serviceId = await api.AddServiceAsync();
        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var to = from.AddDays(90);

        var response = await client.GetAsync(
            $"/api/v1/services/{serviceId}/availability?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task PostMalformedJson_Returns400Not500()
    {
        var (client, _) = await api.CreateSignedInClientAsync();
        var content = new StringContent("{ not valid json", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/bookings", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}
