using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Exsensic.Contracts.Admin;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Catalog;
using Exsensic.Contracts.Common;
using Exsensic.IntegrationTests.Bookings;

namespace Exsensic.IntegrationTests.Security;

/// <summary>
/// The admin catalogue and user journeys through the real API: add, edit and deactivate a service
/// (FR28, FR29), generate, block and unblock time slots (FR26, FR27), and create and deactivate staff
/// accounts (FR32).
/// </summary>
public sealed class AdminCatalogueTests(BookingApiFactory api) : IClassFixture<BookingApiFactory>
{
    [Fact]
    public async Task CreateService_ValidRequest_Returns201AndAppearsInAdminList()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var request = NewService();

        var response = await admin.PostAsJsonAsync("/api/v1/admin/services", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await FindServiceAsync(admin, request.Name);
        Assert.True(created.IsActive);
        Assert.Equal(request.DurationMinutes, created.DurationMinutes);
    }

    [Fact]
    public async Task CreateService_DuplicateName_Returns409Duplicate()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var request = NewService();
        await admin.PostAsJsonAsync("/api/v1/admin/services", request);

        var response = await admin.PostAsJsonAsync("/api/v1/admin/services", request);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, ErrorCodes.Duplicate);
    }

    [Fact]
    public async Task CreateService_DurationOutOfRange_Returns400()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);

        var response = await admin.PostAsJsonAsync("/api/v1/admin/services", NewService() with { DurationMinutes = 5 });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task UpdateService_NewDetails_Returns204AndSavesThem()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var request = NewService();
        await admin.PostAsJsonAsync("/api/v1/admin/services", request);
        var service = await FindServiceAsync(admin, request.Name);

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/services/{service.Id}",
            request with { Description = "Updated by the admin.", DurationMinutes = 90 });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var updated = await FindServiceAsync(admin, request.Name);
        Assert.Equal("Updated by the admin.", updated.Description);
        Assert.Equal(90, updated.DurationMinutes);
    }

    [Fact]
    public async Task UpdateService_UnknownId_Returns404()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);

        var response = await admin.PutAsJsonAsync("/api/v1/admin/services/999999", NewService());

        await AssertProblemAsync(response, HttpStatusCode.NotFound, ErrorCodes.NotFound);
    }

    [Fact]
    public async Task DeactivateService_Returns204AndHidesItFromThePublicCatalogue()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var request = NewService();
        await admin.PostAsJsonAsync("/api/v1/admin/services", request);
        var service = await FindServiceAsync(admin, request.Name);

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/services/{service.Id}/active",
            new SetActiveRequest(false, service.RowVersion));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False((await FindServiceAsync(admin, request.Name)).IsActive);
        var publicList = await api.CreateAnonymousClient().GetFromJsonAsync<ServiceDto[]>("/api/v1/services");
        Assert.DoesNotContain(publicList!, s => s.Id == service.Id);
    }

    [Fact]
    public async Task DeactivateService_StaleRowVersion_Returns409ConcurrencyConflict()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var request = NewService();
        await admin.PostAsJsonAsync("/api/v1/admin/services", request);
        var stale = await FindServiceAsync(admin, request.Name);
        await admin.PutAsJsonAsync($"/api/v1/admin/services/{stale.Id}", request with { Description = "A newer edit." });

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/services/{stale.Id}/active",
            new SetActiveRequest(false, stale.RowVersion));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict);
        Assert.True((await FindServiceAsync(admin, request.Name)).IsActive);
    }

    [Fact]
    public async Task GenerateSlots_SameRequestTwice_CreatesThenSkipsDuplicates()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var date = api.NextFutureDate();
        var request = new GenerateSlotsRequest(date, date, [date.DayOfWeek], [new TimeOnly(9, 0), new TimeOnly(13, 0)], 120);

        var first = await admin.PostAsJsonAsync("/api/v1/admin/timeslots/generate", request);
        var second = await admin.PostAsJsonAsync("/api/v1/admin/timeslots/generate", request);

        Assert.Equal(new GenerateSlotsResult(2, 0), await first.Content.ReadFromJsonAsync<GenerateSlotsResult>());
        Assert.Equal(new GenerateSlotsResult(0, 2), await second.Content.ReadFromJsonAsync<GenerateSlotsResult>());
        Assert.Equal(2, (await SlotsOnAsync(admin, date)).Length);
    }

    [Fact]
    public async Task GenerateSlots_EndBeforeStart_Returns400()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var date = api.NextFutureDate();

        var response = await admin.PostAsJsonAsync("/api/v1/admin/timeslots/generate",
            new GenerateSlotsRequest(date, date.AddDays(-1), [date.DayOfWeek], [new TimeOnly(9, 0)], 120));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task BlockThenUnblockSlot_Returns204AndRecordsTheReason()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var date = api.NextFutureDate();
        var slotId = await api.AddSlotAsync(date, new TimeOnly(9, 0));
        var slot = (await SlotsOnAsync(admin, date)).Single(s => s.Id == slotId);

        var block = await admin.PutAsJsonAsync($"/api/v1/admin/timeslots/{slotId}/block",
            new BlockSlotRequest("Public holiday", slot.RowVersion));

        Assert.Equal(HttpStatusCode.NoContent, block.StatusCode);
        var blocked = (await SlotsOnAsync(admin, date)).Single(s => s.Id == slotId);
        Assert.True(blocked.IsBlocked);
        Assert.Equal("Public holiday", blocked.BlockReason);

        var unblock = await admin.PutAsync($"/api/v1/admin/timeslots/{slotId}/unblock", null);

        Assert.Equal(HttpStatusCode.NoContent, unblock.StatusCode);
        Assert.False((await SlotsOnAsync(admin, date)).Single(s => s.Id == slotId).IsBlocked);
    }

    [Fact]
    public async Task BlockSlot_UnknownId_Returns404()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);

        var response = await admin.PutAsJsonAsync("/api/v1/admin/timeslots/999999/block",
            new BlockSlotRequest("Public holiday", Convert.ToBase64String(new byte[8])));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, ErrorCodes.NotFound);
    }

    [Fact]
    public async Task CreateStaff_ValidRequest_Returns201AndListsThemAsStaff()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var serviceId = await api.AddServiceAsync();
        var email = $"staff-{Guid.NewGuid():N}@example.com";

        var response = await admin.PostAsJsonAsync("/api/v1/admin/users",
            new CreateStaffRequest("New Staff Member", email, $"Staff{Guid.NewGuid():N}", "Photographer", [serviceId]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var staff = await admin.GetFromJsonAsync<PagedResult<UserSummaryDto>>("/api/v1/admin/users?role=Staff&pageSize=100");
        var created = Assert.Single(staff!.Items, u => u.Email == email);
        Assert.Equal(RoleNames.Staff, created.Role);
        Assert.True(created.IsActive);
    }

    [Fact]
    public async Task DeactivateUser_Returns204AndListShowsThemInactive()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);
        var (_, staffId) = await api.CreateSignedInClientAsync(RoleNames.Staff);

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/users/{staffId}/active", new SetUserActiveRequest(false));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var staff = await admin.GetFromJsonAsync<PagedResult<UserSummaryDto>>("/api/v1/admin/users?role=Staff&pageSize=100");
        Assert.False(staff!.Items.Single(u => u.UserId == staffId).IsActive);
    }

    [Fact]
    public async Task DeactivateUser_UnknownId_Returns404()
    {
        var (admin, _) = await api.CreateSignedInClientAsync(RoleNames.Admin);

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/users/{Guid.NewGuid()}/active", new SetUserActiveRequest(false));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static SaveServiceRequest NewService() => new(
        $"Admin test service {Guid.NewGuid():N}",
        "Photoshoot",
        "Created by an admin catalogue test.",
        120,
        1500m);

    private static async Task<ServiceDto> FindServiceAsync(HttpClient admin, string name)
    {
        var services = await admin.GetFromJsonAsync<ServiceDto[]>("/api/v1/admin/services");
        return services!.Single(s => s.Name == name);
    }

    private static async Task<TimeSlotDto[]> SlotsOnAsync(HttpClient admin, DateOnly date) =>
        (await admin.GetFromJsonAsync<TimeSlotDto[]>($"/api/v1/admin/timeslots?from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}"))!;

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}
