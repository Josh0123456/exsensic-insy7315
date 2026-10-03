using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Exsensic.Api.Security;

/// <summary>
/// Rate-limit policies from docs/CONTRACTS.md §2. Applied with [EnableRateLimiting(RateLimitPolicies.Login)].
/// A rejected request gets 429 with a Retry-After header; the error handling turns it into
/// ProblemDetails with the code rate_limited.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>5 login attempts per minute per client IP: slows down password guessing.</summary>
    public const string Login = "login";

    /// <summary>3 registrations per minute per client IP: stops bulk account creation.</summary>
    public const string Register = "register";

    /// <summary>10 booking requests per 10 minutes per signed-in user.</summary>
    public const string BookingCreate = "booking-create";

    /// <summary>Registers the three policies. The client IP is the real one because forwarded headers are trusted (hosting).</summary>
    public static IServiceCollection AddExsensicRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }

                return ValueTask.CompletedTask;
            };

            options.AddPolicy(Login, context => PerKey(ClientIp(context), 5, TimeSpan.FromMinutes(1)));
            options.AddPolicy(Register, context => PerKey(ClientIp(context), 3, TimeSpan.FromMinutes(1)));
            options.AddPolicy(BookingCreate, context => PerKey(
                context.User.FindFirst(ExsensicClaims.UserId)?.Value ?? ClientIp(context), 10, TimeSpan.FromMinutes(10)));
        });

        return services;
    }

    private static RateLimitPartition<string> PerKey(string key, int permits, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = window,
            QueueLimit = 0,
        });

    private static string ClientIp(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
