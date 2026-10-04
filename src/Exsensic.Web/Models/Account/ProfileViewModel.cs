using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Account;

/// <summary>Editable profile presentation populated from the real account adapter.</summary>
public sealed class ProfileViewModel : JourneyPage
{
    /// <summary>FullName for the Web presentation.</summary>
    [Required, Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    /// <summary>CompanyName for the Web presentation.</summary>
    [Display(Name = "Company name")]
    public string? CompanyName { get; set; }

    /// <summary>Email for the Web presentation.</summary>
    [BindNever, Display(Name = "Email address")]
    public string Email { get; set; } = "";

    /// <summary>Phone for the Web presentation.</summary>
    [Phone, Display(Name = "Phone number")]
    public string? Phone { get; set; }

    /// <summary>ShowCompany for the Web presentation.</summary>
    [BindNever]
    public bool ShowCompany { get; set; }
}
