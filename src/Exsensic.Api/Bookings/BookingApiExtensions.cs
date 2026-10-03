using Exsensic.Core.Bookings;

namespace Exsensic.Api.Bookings;

/// <summary>
/// Registers the booking engine together with the pieces only the API can supply, so Program.cs needs
/// one line for everything booking-related.
/// </summary>
public static class BookingApiExtensions
{
    /// <summary>
    /// Adds Core's booking services and observers, the Identity-backed user directory they use for names,
    /// and the booking policy settings from the "BookingPolicy" configuration section.
    /// </summary>
    /// <param name="services">The API's service collection.</param>
    /// <param name="configuration">The API's configuration, for BookingPolicy:ClientCancelCutoffHours.</param>
    public static IServiceCollection AddExsensicBookingApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddExsensicBookings();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<BookingAccessGuard>();
        services.Configure<BookingPolicyOptions>(configuration.GetSection(BookingPolicyOptions.SectionName));

        return services;
    }
}
