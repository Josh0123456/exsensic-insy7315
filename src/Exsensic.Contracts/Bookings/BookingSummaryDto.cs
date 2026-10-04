using Exsensic.Contracts.Enums;

namespace Exsensic.Contracts.Bookings;

/// <summary>
/// One row in a booking list: My bookings, the admin booking list and dashboard, and the staff
/// schedule (docs/CONTRACTS.md §5–6). It carries only what a list or card needs, never the full details.
/// </summary>
/// <param name="Id">The booking's id, used for the "Details" link.</param>
/// <param name="Reference">The human-readable booking reference.</param>
/// <param name="ServiceId">The booked service's id.</param>
/// <param name="ServiceName">The booked service's name.</param>
/// <param name="Category">The booked service's category.</param>
/// <param name="SlotDate">The date of the booking (SAST).</param>
/// <param name="StartTime">The start time of the booking (SAST).</param>
/// <param name="EndTime">The end time of the booking (SAST).</param>
/// <param name="Status">The booking's current status.</param>
/// <param name="ClientName">The client's full name, shown to admins and staff.</param>
/// <param name="ClientCompany">The client's company name, shown to admins and staff.</param>
/// <param name="StaffName">The assigned staff member's name, or null before the booking is confirmed.</param>
/// <param name="RequirementsSummary">A short plain-text summary of the requirements for the staff schedule, or null elsewhere.</param>
public sealed record BookingSummaryDto(
    int Id,
    string Reference,
    int ServiceId,
    string ServiceName,
    ServiceCategory Category,
    DateOnly SlotDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    BookingStatus Status,
    string ClientName,
    string ClientCompany,
    string? StaffName,
    string? RequirementsSummary);
