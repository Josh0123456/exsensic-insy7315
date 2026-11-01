using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exsensic.Contracts.Admin;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Common;
using Exsensic.Data;
using Exsensic.IntegrationTests.Bookings;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Exsensic.IntegrationTests.Security;

/// <summary>
/// An admin can't deactivate their own account, so the system is never left without an admin.
/// Deactivating someone else still works.
/// </summary>
public sealed class AdminSelfLockoutTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    [Fact]
    public async Task Admin_DeactivateSelf_Returns400AndStaysActive()
    {
        var (admin, adminId) = await api.CreateSignedInClientAsync(RoleNames.Admin);

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/users/{adminId}/active", new SetUserActiveRequest(false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ErrorCodes.ValidationFailed, body.RootElement.GetProperty("code").GetString());
        Assert.True(await IsActiveAsync(adminId));
    }

    [Fact]
    public async Task Admin_DeactivateOtherAdmin_Returns204()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (_, otherAdminId) = await api.CreateSignedInClientAsync(RoleNames.Admin);

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/users/{otherAdminId}/active", new SetUserActiveRequest(false));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await IsActiveAsync(otherAdminId));
    }

    private async Task<bool> IsActiveAsync(Guid userId)
    {
        await using var scope = api.NewScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(userId.ToString());
        return user!.IsActive;
    }
}
