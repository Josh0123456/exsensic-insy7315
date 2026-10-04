using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Enums;

namespace Exsensic.Contracts.Admin;

/// <summary>
/// The admin dashboard, returned by GET /api/v1/admin/dashboard (docs/CONTRACTS.md §5–6).
/// </summary>
/// <param name="StatusCounts">How many bookings are in each status, for the dashboard tiles. Every status is present, even at 0.</param>
/// <param name="RequestedWaitingCount">How many Requested bookings are waiting for approval.</param>
/// <param name="Today">Today's bookings (SAST), ordered by start time.</param>
/// <param name="NextSevenDays">Bookings in the 7 days after today, ordered by date and start time.</param>
public sealed record DashboardDto(
    IReadOnlyDictionary<BookingStatus, int> StatusCounts,
    int RequestedWaitingCount,
    IReadOnlyList<BookingSummaryDto> Today,
    IReadOnlyList<BookingSummaryDto> NextSevenDays);
