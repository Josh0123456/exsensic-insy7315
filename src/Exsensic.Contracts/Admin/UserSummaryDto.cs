using System;
using System.Collections.Generic;
using System.Text;

namespace Exsensic.Contracts.Admin;

public sealed record UserSummaryDto(
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    bool IsActive);
