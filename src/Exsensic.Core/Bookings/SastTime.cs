namespace Exsensic.Core.Bookings;

/// <summary>
/// Converts slot dates and times, which are stored in South African local time, to UTC
/// (docs/CONTRACTS.md §4). South Africa has no daylight saving, so the offset is always +2 hours.
/// </summary>
public static class SastTime
{
    /// <summary>South African Standard Time is UTC+2 all year.</summary>
    public static readonly TimeSpan Offset = TimeSpan.FromHours(2);

    /// <summary>
    /// Returns the moment a slot starts or ends, as a UTC time that can be compared with TimeProvider.GetUtcNow().
    /// </summary>
    /// <param name="date">The slot date in SAST.</param>
    /// <param name="time">The slot time in SAST.</param>
    public static DateTimeOffset ToUtc(DateOnly date, TimeOnly time) =>
        new DateTimeOffset(date.ToDateTime(time), Offset).ToUniversalTime();
}
