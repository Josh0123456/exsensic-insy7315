using Exsensic.Core.Bookings.Observers;
using Exsensic.Core.Requirements;
using Microsoft.Extensions.DependencyInjection;

namespace Exsensic.Core.Bookings;

/// <summary>
/// Registers the booking engine, so Program.cs needs one line (docs/CONTRACTS.md: one registration
/// line per owner in shared files).
/// </summary>
public static class BookingServiceExtensions
{
    /// <summary>
    /// Adds the observers and the dispatcher. Observers run in the order registered here:
    /// status history first, so the audit row exists even if a later observer fails.
    /// </summary>
    /// <param name="services">The API's service collection.</param>
    public static IServiceCollection AddExsensicBookings(this IServiceCollection services)
    {
        services.AddScoped<IBookingObserver, StatusHistoryObserver>();
        services.AddScoped<IBookingObserver, NotificationObserver>();
        services.AddScoped<BookingEventDispatcher>();
        services.AddScoped<RequirementTemplateService>();

        return services;
    }
}
