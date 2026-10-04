using Exsensic.Web.ApiClients;
using Exsensic.Web.Models.Bookings;

namespace Exsensic.Web.Journeys;

/// <summary>Web presentation adapter seam. No implementation is registered until the owning shared contracts/client exist.</summary>
public interface IBookingJourney
{
    /// <summary>Loads the entire requested UI grouping before paging; never treats Upcoming or Past as statuses.</summary>
    Task<ApiResult<BookingListViewModel>> MineAsync(string filter, int page, CancellationToken cancellationToken);

    /// <summary>Loads details, requirements, history and display action affordances from the API.</summary>
    Task<ApiResult<BookingDetailViewModel>> DetailAsync(int id, CancellationToken cancellationToken);

    /// <summary>Loads a booking and available slots for that booking's trusted service identifier.</summary>
    Task<ApiResult<RescheduleViewModel>> ReschedulePageAsync(int id, DateOnly from, string? selectedSlotId, CancellationToken cancellationToken);

    /// <summary>Loads a booking and any safely exposed cancellation-policy information.</summary>
    Task<ApiResult<CancelViewModel>> CancelPageAsync(int id, CancellationToken cancellationToken);

    /// <summary>Passes submitted slot and original RowVersion to the API; null means success.</summary>
    Task<ApiProblem?> RescheduleAsync(int id, RescheduleViewModel form, CancellationToken cancellationToken);

    /// <summary>Passes cancellation reason and original RowVersion to the API; null means success.</summary>
    Task<ApiProblem?> CancelAsync(int id, CancelViewModel form, CancellationToken cancellationToken);
}
