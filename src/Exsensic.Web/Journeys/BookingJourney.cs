using System.Globalization;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Common;
using Exsensic.Contracts.Enums;
using Exsensic.Contracts.Requirements;
using Exsensic.Web.ApiClients;
using Exsensic.Web.Models.Bookings;
using Exsensic.Web.Models.Shared;

namespace Exsensic.Web.Journeys;

/// <summary>
/// Connects the client's booking screens to the API (docs/CONTRACTS.md §6): My bookings, booking
/// details and confirmation, reschedule and cancel. The API decides every rule (ownership, cut-off,
/// availability, concurrency); the Can* flags only decide which buttons to show.
/// </summary>
public sealed class BookingJourney(ApiClient api, JourneyClock clock, TimeProvider timeProvider) : IBookingJourney
{
    private const int PageSize = 10;

    /// <summary>The client cut-off shown on the cancel page; the API enforces BookingPolicy:ClientCancelCutoffHours.</summary>
    private const int CancelCutoffHours = 24;

    /// <summary>
    /// Loads all of the client's bookings, then applies the screen's tab: Upcoming (Requested or Confirmed,
    /// not yet started), Requested, Confirmed, Past (completed or already started) or Cancelled.
    /// </summary>
    public async Task<ApiResult<BookingListViewModel>> MineAsync(string filter, int page, CancellationToken cancellationToken)
    {
        var all = new List<BookingSummaryDto>();
        for (var apiPage = 1; ; apiPage++)
        {
            var result = await api.SendAsync<PagedResult<BookingSummaryDto>>(
                HttpMethod.Get, $"api/v1/bookings/mine?page={apiPage}&pageSize=100", cancellationToken);
            if (result.Value is not { } paged) return new(result.StatusCode, default, false, result.Error);
            all.AddRange(paged.Items);
            if (apiPage >= paged.TotalPages) break;
        }

        var matching = all.Where(b => filter switch
            {
                "Requested" => b.Status == BookingStatus.Requested,
                "Confirmed" => b.Status == BookingStatus.Confirmed,
                "Cancelled" => b.Status == BookingStatus.Cancelled,
                "Past" => b.Status == BookingStatus.Completed || (b.Status != BookingStatus.Cancelled && HasStarted(b.SlotDate, b.StartTime)),
                _ => b.Status is BookingStatus.Requested or BookingStatus.Confirmed && !HasStarted(b.SlotDate, b.StartTime),
            })
            .ToList();
        // Upcoming reads soonest first; the other tabs keep the API's newest-first order.
        if (filter == "Upcoming") matching = matching.OrderBy(b => b.SlotDate).ThenBy(b => b.StartTime).ToList();

        var totalPages = Math.Max(1, (int)Math.Ceiling(matching.Count / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);
        var model = new BookingListViewModel
        {
            Filter = filter,
            Page = page,
            Bookings = matching.Skip((page - 1) * PageSize).Take(PageSize).Select(b => ToCard(b)).ToList(),
            Pager = totalPages > 1 ? new PagerViewModel
            {
                CurrentPage = page,
                TotalPages = totalPages,
                PreviousUrl = page > 1 ? $"/Bookings?filter={filter}&page={page - 1}" : null,
                NextUrl = page < totalPages ? $"/Bookings?filter={filter}&page={page + 1}" : null,
                Label = "Booking pages",
            } : null,
            IntegrationAvailable = true,
        };
        return new(200, model, true, null);
    }

    /// <summary>Details, requirements (with their form labels) and the status timeline; 404 if not yours.</summary>
    public async Task<ApiResult<BookingDetailViewModel>> DetailAsync(int id, CancellationToken cancellationToken)
    {
        var result = await api.SendAsync<BookingDetailDto>(HttpMethod.Get, $"api/v1/bookings/{id}", cancellationToken);
        if (result.Value is not { } detail) return new(result.StatusCode, default, false, result.Error);

        return new(result.StatusCode, await ToDetailAsync(detail, cancellationToken), true, null);
    }

    /// <summary>The booking plus slots for the same service, for choosing a new time.</summary>
    public async Task<ApiResult<RescheduleViewModel>> ReschedulePageAsync(int id, DateOnly from, string? selectedSlotId, CancellationToken cancellationToken)
    {
        var detail = await DetailAsync(id, cancellationToken);
        if (detail.Value is not { } booking) return new(detail.StatusCode, default, false, detail.Error);

        var slots = await api.AvailabilityAsync(booking.ServiceId, from, from.AddDays(SlotPickers.WindowDays - 1), cancellationToken);
        if (slots.Value is null) return new(slots.StatusCode, default, false, slots.Error);

        var model = new RescheduleViewModel
        {
            Id = id,
            From = from,
            RowVersion = booking.RowVersion ?? "",
            NewTimeSlotId = selectedSlotId,
            Detail = booking,
            // Posts as NewTimeSlotId, the property the reschedule form reads.
            Picker = SlotPickers.Build(slots.Value, from, nameof(RescheduleViewModel.NewTimeSlotId), selectedSlotId),
            IntegrationAvailable = true,
        };
        return new(200, model, true, null);
    }

    /// <summary>The booking and the cancellation rule, before the client confirms.</summary>
    public async Task<ApiResult<CancelViewModel>> CancelPageAsync(int id, CancellationToken cancellationToken)
    {
        var detail = await DetailAsync(id, cancellationToken);
        if (detail.Value is not { } booking) return new(detail.StatusCode, default, false, detail.Error);

        var model = new CancelViewModel
        {
            Id = id,
            RowVersion = booking.RowVersion ?? "",
            Detail = booking,
            CancellationCutoffHours = CancelCutoffHours,
            IntegrationAvailable = true,
        };
        return new(200, model, true, null);
    }

    /// <summary>PUT /api/v1/bookings/{id}/reschedule with the version the client saw; null on success.</summary>
    public async Task<ApiProblem?> RescheduleAsync(int id, RescheduleViewModel form, CancellationToken cancellationToken)
    {
        if (!int.TryParse(form.NewTimeSlotId, NumberStyles.None, CultureInfo.InvariantCulture, out var slotId))
        {
            return new ApiProblem { StatusCode = 400, Message = "Choose an available time." };
        }

        var result = await api.SendJsonNoContentAsync(HttpMethod.Put, $"api/v1/bookings/{id}/reschedule",
            new RescheduleBookingRequest(slotId, form.RowVersion), cancellationToken);
        return result.Error;
    }

    /// <summary>PUT /api/v1/bookings/{id}/cancel with an optional reason; null on success.</summary>
    public async Task<ApiProblem?> CancelAsync(int id, CancelViewModel form, CancellationToken cancellationToken)
    {
        var reason = string.IsNullOrWhiteSpace(form.Reason) ? null : form.Reason.Trim();
        var result = await api.SendJsonNoContentAsync(HttpMethod.Put, $"api/v1/bookings/{id}/cancel",
            new CancelBookingRequest(reason, form.RowVersion), cancellationToken);
        return result.Error;
    }

    /// <summary>
    /// Maps an API booking to the shared detail model used by client, staff and admin screens.
    /// <paramref name="detailsPath"/> is where booking links point, for example "/Admin/Bookings".
    /// </summary>
    internal async Task<BookingDetailViewModel> ToDetailAsync(BookingDetailDto d, CancellationToken cancellationToken, string detailsPath = "/Bookings")
    {
        // Show requirement answers under the labels the client saw on the form.
        var template = await api.SendAsync<RequirementTemplateDto>(
            HttpMethod.Get, $"api/v1/services/{d.ServiceId}/requirement-template", cancellationToken);
        var labels = template.Value?.Fields.ToDictionary(f => f.Key, f => f.Label) ?? [];

        var active = d.Status is BookingStatus.Requested or BookingStatus.Confirmed;
        return new BookingDetailViewModel
        {
            Id = d.Id,
            ServiceId = d.ServiceId,
            TimeSlotId = d.TimeSlotId,
            RowVersion = d.RowVersion,
            Booking = new BookingCardViewModel
            {
                Reference = d.Reference,
                ServiceName = d.ServiceName,
                Date = d.SlotDate,
                StartTime = d.StartTime,
                EndTime = d.EndTime,
                BookingStatus = d.Status.ToString(),
                AssignedStaff = d.StaffName,
                DetailsUrl = $"{detailsPath}/{d.Id}",
            },
            ClientCompany = d.ClientCompany,
            ContactSummary = string.Join(" · ", new[] { d.ClientName, d.ClientEmail, d.ClientPhone }.Where(p => !string.IsNullOrWhiteSpace(p))),
            CancellationReason = d.CancellationReason,
            Requirements = d.Requirements
                .Select(r => new KeyValuePair<string, string>(labels.GetValueOrDefault(r.Key, r.Key), r.Value))
                .ToList(),
            History = d.History.Select(h => new BookingHistoryViewModel
            {
                Status = h.ToStatus.ToString(),
                ChangedAtUtc = h.ChangedAtUtc,
                ChangedByName = h.ChangedByName,
                Note = h.Note,
            }).ToList(),
            CanReschedule = active && !HasStarted(d.SlotDate, d.StartTime),
            CanCancel = active && !HasStarted(d.SlotDate, d.StartTime),
            IntegrationAvailable = true,
        };
    }

    /// <summary>Maps a booking summary to the shared card; <paramref name="detailsPath"/> is where its link points.</summary>
    internal static BookingCardViewModel ToCard(BookingSummaryDto b, string detailsPath = "/Bookings") => new()
    {
        Reference = b.Reference,
        ServiceName = b.ServiceName,
        Date = b.SlotDate,
        StartTime = b.StartTime,
        EndTime = b.EndTime,
        BookingStatus = b.Status.ToString(),
        AssignedStaff = b.StaffName,
        DetailsUrl = $"{detailsPath}/{b.Id}",
    };

    /// <summary>Whether a slot (SAST date and time) has already started.</summary>
    private bool HasStarted(DateOnly date, TimeOnly start)
    {
        var nowSast = timeProvider.GetUtcNow().ToOffset(TimeSpan.FromHours(2));
        return date < clock.Today || (date == clock.Today && start <= TimeOnly.FromDateTime(nowSast.DateTime));
    }
}
