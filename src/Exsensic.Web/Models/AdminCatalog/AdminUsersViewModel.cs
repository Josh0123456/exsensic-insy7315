using Exsensic.Contracts.Admin;
using Exsensic.Contracts.Common;
using Exsensic.Web.Models.Journeys;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Exsensic.Web.Models.AdminCatalog;

/// <summary>
/// The users list page.
/// </summary>
public sealed class AdminUsersViewModel : JourneyPage
{

    public string? Role { get; set; }

    public int Page { get; set; } = 1;

    [BindNever]
    public int PageSize { get; set; } = 20;

    [BindNever]
    public IReadOnlyList<UserSummaryDto> Users { get; set; } = [];

    [BindNever]
    public int TotalCount { get; set; }
}
