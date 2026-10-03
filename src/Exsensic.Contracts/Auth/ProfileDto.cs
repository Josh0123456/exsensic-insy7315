using System;
using System.Collections.Generic;
using System.Text;

namespace Exsensic.Contracts.Auth;

public sealed record ProfileDto(
    Guid UserId,
    string FullName,
    string Email,
    string? CompanyName,
    string? Phone,
    string Role);
