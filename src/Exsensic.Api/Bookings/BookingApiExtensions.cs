using Exsensic.Core.Bookings;

namespace Exsensic.Api.Bookings;

/// <summary>
/// Registers the booking engine together with the pieces only the API can supply, so Program.cs needs
/// one line for everything booking-related.
/// </summary>
public static class BookingApiExtensions
{
    /// <summary>
    /// Adds Core's booking services and observers, plus the Identity-backed user directory they use for names.
    /// </summary>
    /// <param name="services">The API's service collection.</param>
    public static IServiceCollection AddExsensicBookingApi(this IServiceCollection services)
    {
        services.AddExsensicBookings();
        services.AddScoped<IUserDirectory, UserDirectory>();

        return services;
    }
}
