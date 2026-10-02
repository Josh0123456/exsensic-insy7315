using System.ComponentModel.DataAnnotations;

namespace Exsensic.Contracts.Auth;

public sealed record RegisterRequest(
    [Required, MaxLength(120)] string FullName,
    [Required, MaxLength(150)] string CompanyName,
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(20)] string Phone,
    [Required, MinLength(10)] string Password,
    [Required] string ConfirmPassword);
