using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Enums;
using Exsensic.Contracts.Staff;
using Exsensic.Web.ApiClients;
using Exsensic.Web.Models.Bookings;
using Exsensic.Web.Models.Staff;

namespace Exsensic.Web.Journeys;

/// <summary>
/// Connects the staff workspace to the API (docs/CONTRACTS.md §6): the week's schedule, an assigned
/// booking's details, and marking it completed. The API only returns bookings assigned to the caller
/// and refuses completion before the slot starts.
/// </summary>
public sealed class StaffJourney(IStaffApi api, IBookingsApi bookingApi, BookingJourney bookings, JourneyClock clock, TimeProvider timeProvider) : IStaffJourney
{
    private const string StaffBookings = "/Staff/Bookings";

    /// <summary>The caller's Confirmed and Completed bookings between two SAST dates, soonest first.</summary>
    public async Task<ApiResult<StaffScheduleViewModel>> ScheduleAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var result = await api.ScheduleAsync(from, to, cancellationToken);
        if (result.Value is not { } schedule) return new(result.StatusCode, default, false, result.Error);

        var model = new StaffScheduleViewModel
        {
            WeekStart = from,
            Today = clock.Today,
            Entries = schedule
                .OrderBy(b => b.SlotDate).ThenBy(b => b.StartTime)
                .Select(b => new StaffScheduleItemViewModel { Booking = BookingJourney.ToCard(b, StaffBookings), ClientCompany = b.ClientCompany })
                .ToList(),
            IntegrationAvailable = true,
        };
        return new(result.StatusCode, model, true, null);
    }

    /// <summary>An assigned booking with everything needed to prepare; 404 if it is not assigned to the caller.</summary>
    public async Task<ApiResult<BookingDetailViewModel>> DetailAsync(int id, CancellationToken cancellationToken)
    {
        var result = await bookingApi.DetailAsync(id, cancellationToken);
        if (result.Value is not { } booking) return new(result.StatusCode, default, false, result.Error);

        var detail = await bookings.ToDetailAsync(booking, cancellationToken, StaffBookings);
        // Rescheduling and cancelling are client and admin actions; staff can only complete started work.
        detail.CanReschedule = false;
        detail.CanCancel = false;
        detail.CanComplete = booking.Status == BookingStatus.Confirmed && HasStarted(booking.SlotDate, booking.StartTime);
        return new(result.StatusCode, detail, true, null);
    }

    /// <summary>PUT /api/v1/staff/bookings/{id}/complete with the version the staff member saw; null on success.</summary>
    public async Task<ApiProblem?> CompleteAsync(int id, CompleteBookingViewModel form, CancellationToken cancellationToken)
    {
        var result = await api.CompleteAsync(id,
            new CompleteBookingRequest(form.RowVersion), cancellationToken);
        return result.Error;
    }

    /// <summary>Whether a slot (SAST date and time) has started, so the "Mark as completed" button can show.</summary>
    private bool HasStarted(DateOnly date, TimeOnly start)
    {
        var nowSast = timeProvider.GetUtcNow().ToOffset(TimeSpan.FromHours(2));
        return date < clock.Today || (date == clock.Today && start <= TimeOnly.FromDateTime(nowSast.DateTime));
    }
}
