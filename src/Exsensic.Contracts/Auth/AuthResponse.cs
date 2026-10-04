using System;
using System.Collections.Generic;
using System.Text;

namespace Exsensic.Contracts.Auth;

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    Guid UserId,
    string FullName,
    string Role);
