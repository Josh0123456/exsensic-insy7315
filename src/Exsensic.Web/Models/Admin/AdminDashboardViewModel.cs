using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Admin;

/// <summary>Dashboard presentation; absent data never becomes a fabricated zero.</summary>
public sealed class AdminDashboardViewModel : JourneyPage
{
    /// <summary>StatusCounts for the Web presentation.</summary>
    [BindNever]
    public IReadOnlyDictionary<string,int> StatusCounts { get; set; } = new Dictionary<string,int>();

    /// <summary>WaitingForApproval for the Web presentation.</summary>
    [BindNever]
    public IReadOnlyList<BookingCardViewModel> WaitingForApproval { get; set; } = [];

    /// <summary>Today for the Web presentation.</summary>
    [BindNever]
    public IReadOnlyList<BookingCardViewModel> Today { get; set; } = [];

    /// <summary>NextSevenDays for the Web presentation.</summary>
    [BindNever]
    public IReadOnlyList<BookingCardViewModel> NextSevenDays { get; set; } = [];
}
