using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Exsensic.Contracts.Auth;

public sealed record UpdateProfileRequest(
    [Required, MaxLength(120)] string FullName,
    [MaxLength(150)] string? CompanyName,
    [MaxLength(20)] string? Phone);
