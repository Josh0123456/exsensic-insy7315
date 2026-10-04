using Exsensic.Contracts.Auth;

namespace Exsensic.Web.ApiClients;

/// <summary>Typed access to the merged auth API contracts; the API owns validation and authorization.</summary>
public interface IAuthApi
{
    /// <summary>POST /api/v1/auth/login.</summary>
    Task<ApiResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>POST /api/v1/auth/register.</summary>
    Task<ApiResult<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    /// <summary>GET /api/v1/me.</summary>
    Task<ApiResult<ProfileDto>> ProfileAsync(CancellationToken cancellationToken);

    /// <summary>PUT /api/v1/me.</summary>
    Task<ApiResult<ProfileDto>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken);

}

/// <summary>Sends shared DTOs through the common authenticated transport.</summary>
public sealed class AuthApi(ApiClient api) : IAuthApi
{
    /// <inheritdoc />
    public Task<ApiResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken) =>
        api.SendJsonAsync<LoginRequest, AuthResponse>(HttpMethod.Post, "api/v1/auth/login", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken) =>
        api.SendJsonAsync<RegisterRequest, AuthResponse>(HttpMethod.Post, "api/v1/auth/register", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ProfileDto>> ProfileAsync(CancellationToken cancellationToken) =>
        api.SendAsync<ProfileDto>(HttpMethod.Get, "api/v1/me", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ProfileDto>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        api.SendJsonAsync<UpdateProfileRequest, ProfileDto>(HttpMethod.Put, "api/v1/me", request, cancellationToken);

}
