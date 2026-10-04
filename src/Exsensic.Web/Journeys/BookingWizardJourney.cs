using System.Globalization;
using Exsensic.Contracts.Auth;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Catalog;
using Exsensic.Contracts.Requirements;
using Exsensic.Web.ApiClients;
using Exsensic.Web.Models.Book;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Journeys;

/// <summary>
/// Connects the two-step booking wizard to the API (docs/CONTRACTS.md §6): available slots for a
/// service, the service category's requirement form, and POST /api/v1/bookings. The API decides
/// availability and validates the requirements; this class only shapes the screens.
/// </summary>
public sealed class BookingWizardJourney(ApiClient api, JourneyClock clock) : IBookingWizardJourney
{
    private const int WindowDays = 14;

    /// <summary>The longest look-ahead the API allows for availability (ServiceCatalogService.MaxAvailabilityDays).</summary>
    private const int MaxLookAheadDays = 60;

    /// <summary>Step 1: the service and its bookable slots for 14 days from <paramref name="from"/>.</summary>
    public async Task<ApiResult<SlotStepViewModel>> SlotsAsync(int serviceId, DateOnly from, string? selectedSlotId, CancellationToken cancellationToken)
    {
        var service = await api.SendAsync<ServiceDto>(HttpMethod.Get, $"api/v1/services/{serviceId}", cancellationToken);
        if (service.Value is null) return Fail<SlotStepViewModel>(service.StatusCode, service.Error);

        var to = from.AddDays(WindowDays - 1);
        var slots = await AvailabilityAsync(serviceId, from, to, cancellationToken);
        if (slots.Value is null) return Fail<SlotStepViewModel>(slots.StatusCode, slots.Error);

        var dates = Enumerable.Range(0, WindowDays)
            .Select(offset => from.AddDays(offset))
            .Select(date =>
            {
                var daySlots = slots.Value.Where(s => s.SlotDate == date)
                    .Select(s => new SlotOptionViewModel
                    {
                        Id = s.TimeSlotId.ToString(CultureInfo.InvariantCulture),
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                    })
                    .ToList();
                return new SlotDateViewModel { Date = date, IsAvailable = daySlots.Count > 0, Slots = daySlots };
            })
            .ToList();

        var model = new SlotStepViewModel
        {
            ServiceId = serviceId,
            From = from,
            TimeSlotId = selectedSlotId,
            Service = ToCard(service.Value),
            Picker = new SlotPickerViewModel
            {
                Id = "slot-picker",
                // Must match SlotStepViewModel.TimeSlotId so the chosen slot is posted back.
                InputName = nameof(SlotStepViewModel.TimeSlotId),
                SelectedSlotId = selectedSlotId,
                SelectedDate = slots.Value.FirstOrDefault(s => s.TimeSlotId.ToString(CultureInfo.InvariantCulture) == selectedSlotId)?.SlotDate,
                Dates = dates,
            },
            IntegrationAvailable = true,
        };
        return Ok(service, model);
    }

    /// <summary>
    /// Step 2: the requirement fields for the service's category, a summary of the chosen slot, and the
    /// client's contact details. A slot that is no longer available returns 409 slot_unavailable.
    /// </summary>
    public async Task<ApiResult<RequirementsStepViewModel>> RequirementsAsync(int serviceId, string timeSlotId, CancellationToken cancellationToken)
    {
        var service = await api.SendAsync<ServiceDto>(HttpMethod.Get, $"api/v1/services/{serviceId}", cancellationToken);
        if (service.Value is null) return Fail<RequirementsStepViewModel>(service.StatusCode, service.Error);

        var template = await api.SendAsync<RequirementTemplateDto>(
            HttpMethod.Get, $"api/v1/services/{serviceId}/requirement-template", cancellationToken);
        if (template.Value is null) return Fail<RequirementsStepViewModel>(template.StatusCode, template.Error);

        var slots = await AvailabilityAsync(serviceId, clock.Today, clock.Today.AddDays(MaxLookAheadDays - 1), cancellationToken);
        if (slots.Value is null) return Fail<RequirementsStepViewModel>(slots.StatusCode, slots.Error);

        var slot = slots.Value.FirstOrDefault(s => s.TimeSlotId.ToString(CultureInfo.InvariantCulture) == timeSlotId);
        if (slot is null)
        {
            return new ApiResult<RequirementsStepViewModel>(409, default, false, new ApiProblem
            {
                StatusCode = 409,
                Code = "slot_unavailable",
                Message = "That time is no longer available. Please choose another time.",
            });
        }

        var profile = await api.SendAsync<ProfileDto>(HttpMethod.Get, "api/v1/me", cancellationToken);
        if (profile.Value is null) return Fail<RequirementsStepViewModel>(profile.StatusCode, profile.Error);

        var model = new RequirementsStepViewModel
        {
            ServiceId = serviceId,
            TimeSlotId = timeSlotId,
            ServiceName = service.Value.Name,
            SlotSummary = $"{slot.SlotDate:dddd, d MMMM yyyy} · {slot.StartTime:HH:mm}–{slot.EndTime:HH:mm} SAST",
            ContactSummary = string.Join(" · ", new[] { profile.Value.FullName, profile.Value.CompanyName, profile.Value.Email, profile.Value.Phone }
                .Where(part => !string.IsNullOrWhiteSpace(part))),
            Fields = template.Value.Fields.Select(f => new RequirementInputViewModel
            {
                Key = f.Key,
                Label = f.Label,
                InputType = f.InputType,
                Required = f.Required,
                MaxLength = f.MaxLength,
                Min = f.Min,
                Max = f.Max,
                Options = f.Options,
            }).ToList(),
            IntegrationAvailable = true,
        };
        return Ok(template, model);
    }

    /// <summary>Submits the booking request; on success returns the new booking's id.</summary>
    public async Task<ApiResult<int>> SubmitAsync(RequirementsStepViewModel form, CancellationToken cancellationToken)
    {
        if (!int.TryParse(form.TimeSlotId, NumberStyles.None, CultureInfo.InvariantCulture, out var timeSlotId))
        {
            return new ApiResult<int>(400, 0, false, new ApiProblem { StatusCode = 400, Message = "Choose an available time before submitting." });
        }

        // Send only the fields the form showed; the API rejects unknown keys anyway.
        var keys = form.Fields.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        var requirements = form.Requirements
            .Where(pair => keys.Contains(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value.Trim());

        var result = await api.SendJsonAsync<CreateBookingRequest, BookingCreatedDto>(
            HttpMethod.Post, "api/v1/bookings", new CreateBookingRequest(form.ServiceId, timeSlotId, requirements), cancellationToken);

        return result.Value is { } created
            ? new ApiResult<int>(result.StatusCode, created.Id, true, null)
            : new ApiResult<int>(result.StatusCode, 0, false, result.Error);
    }

    private Task<ApiResult<List<AvailableSlotDto>>> AvailabilityAsync(int serviceId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        api.SendAsync<List<AvailableSlotDto>>(HttpMethod.Get,
            $"api/v1/services/{serviceId}/availability?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", cancellationToken);

    private static ServiceCardViewModel ToCard(ServiceDto s) => new()
    {
        ServiceId = s.Id.ToString(CultureInfo.InvariantCulture),
        Name = s.Name,
        Category = s.Category,
        Description = s.Description,
        DurationMinutes = s.DurationMinutes,
        StartingPrice = s.BasePrice,
    };

    private static ApiResult<T> Ok<T, TSource>(ApiResult<TSource> source, T value) => new(source.StatusCode, value, true, null);

    private static ApiResult<T> Fail<T>(int? status, ApiProblem? error) => new(status, default, false, error);
}
