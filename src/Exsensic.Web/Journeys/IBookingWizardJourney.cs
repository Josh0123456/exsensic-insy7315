using Exsensic.Web.ApiClients;
using Exsensic.Web.Models.Book;

namespace Exsensic.Web.Journeys;

/// <summary>Web presentation adapter seam. No implementation is registered until the owning shared contracts/client exist.</summary>
public interface IBookingWizardJourney
{
    /// <summary>Loads exactly 14 days of real availability and service presentation through P3's client.</summary>
    Task<ApiResult<SlotStepViewModel>> SlotsAsync(int serviceId, DateOnly from, string? selectedSlotId, CancellationToken cancellationToken);

    /// <summary>Reloads trusted slot/service/profile data and real requirement-template metadata.</summary>
    Task<ApiResult<RequirementsStepViewModel>> RequirementsAsync(int serviceId, string timeSlotId, CancellationToken cancellationToken);

    /// <summary>Creates a booking with the real shared DTO and returns only its API-assigned identifier.</summary>
    Task<ApiResult<int>> SubmitAsync(RequirementsStepViewModel form, CancellationToken cancellationToken);
}
