using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Account;

/// <summary>Public client registration form; no role selection.</summary>
public sealed class RegisterViewModel : JourneyPage
{
    /// <summary>FullName for the Web presentation.</summary>
    [Required, Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    /// <summary>CompanyName for the Web presentation.</summary>
    [Required, Display(Name = "Company name")]
    public string CompanyName { get; set; } = "";

    /// <summary>Email for the Web presentation.</summary>
    [Required, EmailAddress, Display(Name = "Email address")]
    public string Email { get; set; } = "";

    /// <summary>Phone for the Web presentation.</summary>
    [Required, Phone, Display(Name = "Phone number")]
    public string Phone { get; set; } = "";

    /// <summary>Password for the Web presentation.</summary>
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    /// <summary>ConfirmPassword for the Web presentation.</summary>
    [Required, DataType(DataType.Password), Compare(nameof(Password)), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";
}
