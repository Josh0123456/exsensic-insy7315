namespace Exsensic.Web.Journeys;

/// <summary>Supplies SAST dates for calendar presentation only, never booking policy decisions.</summary>
/// <param name="timeProvider">Injectable UTC clock.</param>
public sealed class JourneyClock(TimeProvider timeProvider)
{
    /// <summary>Current South African date; SAST is UTC+02:00.</summary>
    public DateOnly Today => DateOnly.FromDateTime(timeProvider.GetUtcNow().ToOffset(TimeSpan.FromHours(2)).DateTime);

    /// <summary>Keeps previous/next calendar links within representable dates; not an availability rule.</summary>
    public DateOnly WindowStart(DateOnly? date)
    {
        var value = date ?? Today;
        return DateOnly.FromDayNumber(Math.Clamp(value.DayNumber, 14, DateOnly.MaxValue.DayNumber - 14));
    }

    /// <summary>Returns the Monday for a displayed week.</summary>
    public DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
