using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Exsensic.Contracts.Admin;

public sealed record CreateStaffRequest(
    [Required, MaxLength(120)] string FullName,
    [Required, EmailAddress] string Email,
    [Required, MinLength(10)] string Password,
    [Required, MaxLength(100)] string JobTitle,
    int[] ServiceIds);
