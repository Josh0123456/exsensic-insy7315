using Exsensic.Contracts.Auth;
using Exsensic.Web.ApiClients;
using Exsensic.Web.Models.Account;

namespace Exsensic.Web.Journeys;

/// <summary>
/// Connects the account screens to the API (docs/CONTRACTS.md §6): register, sign in and profile.
/// On a successful register or sign-in, the API's access token is kept in the encrypted sign-in
/// cookie (via <see cref="AccountSession"/>); BearerTokenHandler then sends it with every API call.
/// No rules are decided here: the API validates everything and its errors are shown on the form.
/// </summary>
public sealed class AccountJourney(ApiClient api, AccountSession session) : IAccountJourney
{
    /// <summary>Signs in through POST /api/v1/auth/login and starts the cookie session on success.</summary>
    public async Task<ApiProblem?> LoginAsync(LoginViewModel form, CancellationToken cancellationToken)
    {
        var result = await api.SendJsonAsync<LoginRequest, AuthResponse>(
            HttpMethod.Post, "api/v1/auth/login", new LoginRequest(form.Email, form.Password), cancellationToken);
        return await StartSessionAsync(result, cancellationToken);
    }

    /// <summary>Registers a Client through POST /api/v1/auth/register and signs them in on success.</summary>
    public async Task<ApiProblem?> RegisterAsync(RegisterViewModel form, CancellationToken cancellationToken)
    {
        var request = new RegisterRequest(
            form.FullName, form.CompanyName, form.Email, form.Phone, form.Password, form.ConfirmPassword);
        var result = await api.SendJsonAsync<RegisterRequest, AuthResponse>(
            HttpMethod.Post, "api/v1/auth/register", request, cancellationToken);
        return await StartSessionAsync(result, cancellationToken);
    }

    /// <summary>Loads the signed-in user's profile from GET /api/v1/me.</summary>
    public async Task<ApiResult<ProfileViewModel>> GetProfileAsync(CancellationToken cancellationToken)
    {
        var result = await api.SendAsync<ProfileDto>(HttpMethod.Get, "api/v1/me", cancellationToken);
        if (result.Value is not { } profile)
        {
            return new ApiResult<ProfileViewModel>(result.StatusCode, default, false, result.Error);
        }

        var model = new ProfileViewModel
        {
            FullName = profile.FullName,
            CompanyName = profile.CompanyName,
            Email = profile.Email,
            Phone = profile.Phone,
            ShowCompany = profile.Role == RoleNames.Client,
            IntegrationAvailable = true,
        };
        return new ApiResult<ProfileViewModel>(result.StatusCode, model, true, null);
    }

    /// <summary>Saves name, company and phone through PUT /api/v1/me; email cannot be changed here.</summary>
    public async Task<ApiProblem?> SaveProfileAsync(ProfileViewModel form, CancellationToken cancellationToken)
    {
        var request = new UpdateProfileRequest(form.FullName, form.CompanyName, form.Phone);
        var result = await api.SendJsonNoContentAsync(HttpMethod.Put, "api/v1/me", request, cancellationToken);
        return result.Error;
    }

    private async Task<ApiProblem?> StartSessionAsync(ApiResult<AuthResponse> result, CancellationToken cancellationToken)
    {
        if (result.Value is not { } auth)
        {
            return result.Error ?? new ApiProblem { StatusCode = result.StatusCode, Message = "We could not sign you in. Please try again." };
        }

        await session.SignInAsync(auth.UserId, auth.FullName, auth.Role, auth.AccessToken, auth.ExpiresAtUtc, cancellationToken);
        return null;
    }
}
