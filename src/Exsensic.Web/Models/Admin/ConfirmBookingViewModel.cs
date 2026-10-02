using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Admin;

/// <summary>Admin confirmation input; available staff are supplied only by the API.</summary>
public sealed class ConfirmBookingViewModel
{
    /// <summary>RowVersion for the Web presentation.</summary>
    [Required]
    public string RowVersion { get; set; } = "";

    /// <summary>StaffUserId for the Web presentation.</summary>
    [Required(ErrorMessage = "Choose an available staff member."), Display(Name = "Assign staff")]
    public string? StaffUserId { get; set; }
}
