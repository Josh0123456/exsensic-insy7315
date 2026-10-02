using Exsensic.Contracts.Enums;

namespace Exsensic.Contracts.Bookings;

/// <summary>
/// Everything about one booking, returned by GET /api/v1/bookings/{id} and by every action that
/// changes a booking: reschedule, cancel, confirm, reject and complete (docs/CONTRACTS.md §5–6).
/// Only the owner client, the assigned staff member or an admin can receive it.
/// </summary>
/// <param name="Id">The booking's id.</param>
/// <param name="Reference">The human-readable booking reference.</param>
/// <param name="ServiceId">The booked service's id.</param>
/// <param name="ServiceName">The booked service's name.</param>
/// <param name="Category">The booked service's category, which decides the requirement template.</param>
/// <param name="TimeSlotId">The booked time slot's id.</param>
/// <param name="SlotDate">The date of the booking (SAST).</param>
/// <param name="StartTime">The start time of the booking (SAST).</param>
/// <param name="EndTime">The end time of the booking (SAST).</param>
/// <param name="Status">The booking's current status.</param>
/// <param name="ClientName">The client's full name.</param>
/// <param name="ClientCompany">The client's company name.</param>
/// <param name="ClientEmail">The client's email address, so staff can prepare and get in touch.</param>
/// <param name="ClientPhone">The client's phone number.</param>
/// <param name="StaffUserId">The assigned staff member's user id, or null before the booking is confirmed.</param>
/// <param name="StaffName">The assigned staff member's name, or null before the booking is confirmed.</param>
/// <param name="CancellationReason">Why the booking was cancelled; starts with "Rejected: " for a rejection. Null otherwise.</param>
/// <param name="Requirements">The client's requirement answers, keyed by requirement field key.</param>
/// <param name="History">The status timeline, oldest first.</param>
/// <param name="CreatedAtUtc">When the booking was created, in UTC.</param>
/// <param name="UpdatedAtUtc">When the booking was last changed, in UTC.</param>
/// <param name="RowVersion">The concurrency token as a base64 string; send it back with any change.</param>
public sealed record BookingDetailDto(
    int Id,
    string Reference,
    int ServiceId,
    string ServiceName,
    ServiceCategory Category,
    int TimeSlotId,
    DateOnly SlotDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    BookingStatus Status,
    string ClientName,
    string ClientCompany,
    string ClientEmail,
    string ClientPhone,
    Guid? StaffUserId,
    string? StaffName,
    string? CancellationReason,
    IReadOnlyDictionary<string, string> Requirements,
    IReadOnlyList<BookingStatusHistoryDto> History,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string RowVersion);
