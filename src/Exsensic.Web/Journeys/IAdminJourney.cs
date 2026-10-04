using Exsensic.Web.ApiClients;
using Exsensic.Web.Models.Admin;

namespace Exsensic.Web.Journeys;

/// <summary>Web presentation adapter seam. No implementation is registered until the owning shared contracts/client exist.</summary>
public interface IAdminJourney
{
    /// <summary>Loads real dashboard counts and sections; never synthesizes zero counts on failure.</summary>
    Task<ApiResult<AdminDashboardViewModel>> DashboardAsync(CancellationToken cancellationToken);

    /// <summary>Loads filtered, paged admin bookings from the real API.</summary>
    Task<ApiResult<AdminBookingListViewModel>> ListAsync(string? status, DateOnly? from, DateOnly? to, int page, CancellationToken cancellationToken);

    /// <summary>Loads a booking and qualified/free staff from their API endpoints.</summary>
    Task<ApiResult<AdminReviewViewModel>> ReviewAsync(int id, CancellationToken cancellationToken);

    /// <summary>Confirms through the API with an explicitly selected staff ID and original concurrency token.</summary>
    Task<ApiProblem?> ConfirmAsync(int id, ConfirmBookingViewModel form, CancellationToken cancellationToken);

    /// <summary>Rejects through the API with a reason and original concurrency token.</summary>
    Task<ApiProblem?> RejectAsync(int id, RejectBookingViewModel form, CancellationToken cancellationToken);
}
