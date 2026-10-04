using Exsensic.Contracts.Admin;
using Exsensic.Contracts.Common;

namespace Exsensic.Web.ApiClients;

/// <summary>
/// Typed client for the admin user-management endpoints.
/// </summary>
public interface IAdminUsersApi
{
    /// <summary>GET /api/v1/admin/users: one page of users, optionally filtered by role.</summary>
    Task<ApiResult<PagedResult<UserSummaryDto>>> ListAsync(string? role, int page, CancellationToken ct);

    /// <summary>POST /api/v1/admin/users: creates a Staff account with a profile and service links.</summary>
    Task<ApiResult<object>> CreateStaffAsync(CreateStaffRequest request, CancellationToken ct);

    /// <summary>PUT /api/v1/admin/users/{id}/active: activates or deactivates an account.</summary>
    Task<ApiResult<object>> SetActiveAsync(Guid userId, SetUserActiveRequest request, CancellationToken ct);
}
