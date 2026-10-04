using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Staff;

/// <summary>Completion form carries only concurrency state; authorization remains at the API.</summary>
public sealed class CompleteBookingViewModel
{
    /// <summary>RowVersion for the Web presentation.</summary>
    [Required]
    public string RowVersion { get; set; } = "";
}
