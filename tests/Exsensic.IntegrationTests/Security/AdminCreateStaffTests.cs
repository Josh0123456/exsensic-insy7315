using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exsensic.Contracts.Admin;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Common;
using Exsensic.IntegrationTests.Bookings;

namespace Exsensic.IntegrationTests.Security;

/// <summary>
/// Creating a staff account reports the real reason it failed: a weak password is a 400 against the
/// Password field, and only an email that is already registered is a 409 duplicate.
/// </summary>
public sealed class AdminCreateStaffTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    [Fact]
    public async Task CreateStaff_WeakPassword_Returns400WithPasswordError()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);

        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", NewStaff("alllowercase"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ErrorCodes.ValidationFailed, body.RootElement.GetProperty("code").GetString());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("Password", out _));
    }

    [Fact]
    public async Task CreateStaff_ExistingEmail_Returns409Duplicate()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var request = NewStaff($"Staff{Guid.NewGuid():N}");
        await admin.PostAsJsonAsync("/api/v1/admin/users", request);

        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ErrorCodes.Duplicate, body.RootElement.GetProperty("code").GetString());
    }

    private static CreateStaffRequest NewStaff(string password) =>
        new("New Staff Member", $"staff-{Guid.NewGuid():N}@example.com", password, "Photographer", []);
}
