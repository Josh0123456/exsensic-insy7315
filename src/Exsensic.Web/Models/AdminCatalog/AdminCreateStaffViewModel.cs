using System.ComponentModel.DataAnnotations;
using Exsensic.Contracts.Catalog;
using Exsensic.Web.Models.Journeys;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Exsensic.Web.Models.AdminCatalog;

/// <summary>
/// The create-staff form.
/// </summary>
public sealed class AdminCreateStaffViewModel : JourneyPage
{
   
    [Required, MaxLength(120), Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required, EmailAddress, Display(Name = "Email")]
    public string Email { get; set; } = "";

    [Required, MinLength(10), DataType(DataType.Password), Display(Name = "Password")]
    public string Password { get; set; } = "";

    [Required, MaxLength(100), Display(Name = "Job title")]
    public string JobTitle { get; set; } = "";

    [Display(Name = "Services they can deliver")]
    public int[] ServiceIds { get; set; } = [];

    [BindNever]
    public IReadOnlyList<ServiceDto> AvailableServices { get; set; } = [];
}
