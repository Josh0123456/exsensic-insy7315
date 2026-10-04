using Exsensic.Web.ApiClients;
using Exsensic.Web.Models.Account;

namespace Exsensic.Web.Journeys;

/// <summary>Web presentation adapter seam. No implementation is registered until the owning shared contracts/client exist.</summary>
public interface IAccountJourney
{
    /// <summary>Loads a profile using P1's auth client using P3's real contracts.</summary>
    Task<ApiResult<ProfileViewModel>> GetProfileAsync(CancellationToken cancellationToken);

    /// <summary>Authenticates with the real API and establishes a cookie only on success; returns null on success.</summary>
    Task<ApiProblem?> LoginAsync(LoginViewModel form, CancellationToken cancellationToken);

    /// <summary>Registers a real Client and establishes the session only on API success.</summary>
    Task<ApiProblem?> RegisterAsync(RegisterViewModel form, CancellationToken cancellationToken);

    /// <summary>Saves the profile with the API; returns null only on success.</summary>
    Task<ApiProblem?> SaveProfileAsync(ProfileViewModel form, CancellationToken cancellationToken);
}
