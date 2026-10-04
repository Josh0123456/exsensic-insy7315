using Exsensic.Api.Security;
using Exsensic.Contracts.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Exsensic.Api.Controllers;

/// <summary>The signed-in user's own profile (docs/CONTRACTS.md §6). Any signed-in role may use it.</summary>
[ApiController]
[Route("api/v1/me")]
public sealed class MeController(AccountService accounts) : ControllerBase
{
    /// <summary>Returns the caller's profile.</summary>
    [HttpGet]
    public Task<ProfileDto> Get(CancellationToken ct) => accounts.GetProfileAsync(User.GetUserId(), ct);

    /// <summary>Updates the caller's name, and company and phone for clients.</summary>
    [HttpPut]
    public Task<ProfileDto> Update(UpdateProfileRequest request, CancellationToken ct) =>
        accounts.UpdateProfileAsync(User.GetUserId(), request, ct);
}
