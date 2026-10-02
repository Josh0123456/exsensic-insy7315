using Exsensic.Web.ApiClients;
using Exsensic.Web.Models.Bookings;
using Exsensic.Web.Models.Staff;

namespace Exsensic.Web.Journeys;

/// <summary>Web presentation adapter seam. No implementation is registered until the owning shared contracts/client exist.</summary>
public interface IStaffJourney
{
    /// <summary>Loads the API-backed week; dates and display text are already SAST.</summary>
    Task<ApiResult<StaffScheduleViewModel>> ScheduleAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>Loads an assigned booking through the real API, preserving its access controls.</summary>
    Task<ApiResult<BookingDetailViewModel>> DetailAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Submits the original RowVersion to the completion endpoint; null means success.</summary>
    Task<ApiProblem?> CompleteAsync(Guid id, CompleteBookingViewModel form, CancellationToken cancellationToken);
}
