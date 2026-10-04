using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Admin;

/// <summary>Admin filters and responsive booking-list composition.</summary>
public sealed class AdminBookingListViewModel : JourneyPage
{
    /// <summary>Status for the Web presentation.</summary>
    public string? Status { get; set; }

    /// <summary>From for the Web presentation.</summary>
    public DateOnly? From { get; set; }

    /// <summary>To for the Web presentation.</summary>
    public DateOnly? To { get; set; }

    /// <summary>Page for the Web presentation.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Rows for the Web presentation.</summary>
    [BindNever]
    public IReadOnlyList<AdminBookingRowViewModel> Rows { get; set; } = [];

    /// <summary>Pager for the Web presentation.</summary>
    [BindNever]
    public PagerViewModel? Pager { get; set; }
}
