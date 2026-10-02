using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Exsensic.Web.Models.Journeys;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Models.Account;

/// <summary>Login form only; contains no token or API response.</summary>
public sealed class LoginViewModel : JourneyPage
{
    /// <summary>Email for the Web presentation.</summary>
    [Required, EmailAddress, Display(Name = "Email address")]
    public string Email { get; set; } = "";

    /// <summary>Password for the Web presentation.</summary>
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    /// <summary>ReturnUrl for the Web presentation.</summary>
    public string? ReturnUrl { get; set; }
}
