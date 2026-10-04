using System.Globalization;
using Exsensic.Contracts.Catalog;
using Exsensic.Web.ApiClients;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Journeys;

/// <summary>
/// Loads available slots from the API and shapes them into the shared slot picker, for the booking
/// wizard and for rescheduling. Availability itself is decided by the API.
/// </summary>
public static class SlotPickers
{
    /// <summary>How many days one picker shows.</summary>
    public const int WindowDays = 14;

    /// <summary>GET /api/v1/services/{id}/availability for an inclusive date range.</summary>
    public static Task<ApiResult<List<AvailableSlotDto>>> AvailabilityAsync(
        this ApiClient api, int serviceId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        api.SendAsync<List<AvailableSlotDto>>(HttpMethod.Get,
            $"api/v1/services/{serviceId}/availability?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", cancellationToken);

    /// <summary>
    /// Builds a picker for <see cref="WindowDays"/> days from <paramref name="from"/>. <paramref name="inputName"/>
    /// must match the form property that receives the chosen slot id.
    /// </summary>
    public static SlotPickerViewModel Build(IReadOnlyList<AvailableSlotDto> slots, DateOnly from, string inputName, string? selectedSlotId) => new()
    {
        Id = "slot-picker",
        InputName = inputName,
        SelectedSlotId = selectedSlotId,
        SelectedDate = slots.FirstOrDefault(s => Id(s) == selectedSlotId)?.SlotDate,
        Dates = Enumerable.Range(0, WindowDays).Select(offset =>
        {
            var date = from.AddDays(offset);
            var daySlots = slots.Where(s => s.SlotDate == date)
                .Select(s => new SlotOptionViewModel { Id = Id(s), StartTime = s.StartTime, EndTime = s.EndTime })
                .ToList();
            return new SlotDateViewModel { Date = date, IsAvailable = daySlots.Count > 0, Slots = daySlots };
        }).ToList(),
    };

    private static string Id(AvailableSlotDto slot) => slot.TimeSlotId.ToString(CultureInfo.InvariantCulture);
}
