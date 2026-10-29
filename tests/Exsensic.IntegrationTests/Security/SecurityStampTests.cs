using System.Net;
using System.Net.Http.Json;
using Exsensic.Contracts.Admin;
using Exsensic.Contracts.Auth;
using Exsensic.IntegrationTests.Bookings;

namespace Exsensic.IntegrationTests.Security;

/// <summary>
/// A JWT carries the user's security stamp. When an admin deactivates the account the stamp changes, so a
/// token issued before that stops working straight away instead of living until it expires.
/// </summary>
public sealed class SecurityStampTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    [Fact]
    public async Task DeactivatedUser_ExistingToken_Returns401()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (staff, staffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/v1/me")).StatusCode);

        await admin.PutAsJsonAsync($"/api/v1/admin/users/{staffId}/active", new SetUserActiveRequest(false));

        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task ReactivatedUser_TokenFromBeforeDeactivation_StillReturns401()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (staff, staffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);

        await admin.PutAsJsonAsync($"/api/v1/admin/users/{staffId}/active", new SetUserActiveRequest(false));
        await admin.PutAsJsonAsync($"/api/v1/admin/users/{staffId}/active", new SetUserActiveRequest(true));

        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync("/api/v1/me")).StatusCode);
        var freshToken = await api.SignInAsAsync(staffId, RoleNames.Staff);
        Assert.Equal(HttpStatusCode.OK, (await freshToken.GetAsync("/api/v1/me")).StatusCode);
    }
}
