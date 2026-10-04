using Exsensic.Contracts.Admin;
using Exsensic.Contracts.Common;

namespace Exsensic.Web.ApiClients;

/// <summary>
/// The <see cref="IAdminUsersApi"/> implementation over the shared JSON transport.
/// </summary>
public sealed class AdminUsersApi(ApiClient api) : IAdminUsersApi
{
    /// <inheritdoc />
    public Task<ApiResult<PagedResult<UserSummaryDto>>> ListAsync(string? role, int page, CancellationToken ct)
    {
        var query = $"api/v1/admin/users?page={page}&pageSize=20";
        if (!string.IsNullOrWhiteSpace(role))
        {
            query += $"&role={Uri.EscapeDataString(role)}";
        }

        return api.SendAsync<PagedResult<UserSummaryDto>>(HttpMethod.Get, query, ct);
    }

    /// <inheritdoc />
    public Task<ApiResult<object>> CreateStaffAsync(CreateStaffRequest request, CancellationToken ct) =>
        api.SendJsonNoContentAsync(HttpMethod.Post, "api/v1/admin/users", request, ct);

    /// <inheritdoc />
    public Task<ApiResult<object>> SetActiveAsync(Guid userId, SetUserActiveRequest request, CancellationToken ct) =>
        api.SendJsonNoContentAsync(HttpMethod.Put, $"api/v1/admin/users/{userId}/active", request, ct);
}
