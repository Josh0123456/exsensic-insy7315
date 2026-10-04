using Exsensic.Contracts.Admin;
using Exsensic.Web.Models.Journeys;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Exsensic.Web.Models.Admin;

/// <summary>
/// The /Admin/Users page. Holds the current role filter and the users returned by the API.
/// </summary>
public sealed class UserManagementViewModel : JourneyPage
{
    /// <summary>The role filter, empty for all.</summary>
    public string? Role { get; set; }

    /// <summary>The users returned by the API for the current filter.</summary>
    [BindNever]
    public IReadOnlyList<UserSummaryDto> Users { get; set; } = [];
}
