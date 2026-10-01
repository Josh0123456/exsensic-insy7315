using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Admin;

/// <summary>Rejection input; the API validates the reason and resulting cancellation.</summary>
public sealed class RejectBookingViewModel
{
    /// <summary>RowVersion for the Web presentation.</summary>
    [Required]
    public string RowVersion { get; set; } = "";

    /// <summary>Reason for the Web presentation.</summary>
    [Required(ErrorMessage = "Explain why this request cannot be accepted."), Display(Name = "Reason for rejection")]
    public string Reason { get; set; } = "";
}
