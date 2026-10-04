using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Bookings;

/// <summary>Client booking collection; grouping applies before pagination in the real adapter.</summary>
public sealed class BookingListViewModel : JourneyPage
{
    /// <summary>Filter for the Web presentation.</summary>
    public string Filter { get; set; } = "Upcoming";

    /// <summary>Page for the Web presentation.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Bookings for the Web presentation.</summary>
    [BindNever]
    public IReadOnlyList<BookingCardViewModel> Bookings { get; set; } = [];

    /// <summary>Pager for the Web presentation.</summary>
    [BindNever]
    public PagerViewModel? Pager { get; set; }
}
