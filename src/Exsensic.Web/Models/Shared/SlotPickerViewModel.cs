namespace Exsensic.Web.Models.Shared;

/// <summary>Date and slot options supplied by the caller; no availability calculation.</summary>
public sealed class SlotPickerViewModel
{
    /// <summary>Unique HTML ID for this picker.</summary>
    public required string Id { get; init; }

    /// <summary>Radio field name expected by the parent form; unique per picker.</summary>
    public string InputName { get; init; } = "SlotId";

    /// <summary>Date to display initially; must match a supplied enabled date.</summary>
    public DateOnly? SelectedDate { get; init; }

    /// <summary>Optional initially selected enabled slot on the displayed date.</summary>
    public string? SelectedSlotId { get; init; }

    /// <summary>Available and unavailable date options in display order.</summary>
    public IReadOnlyList<SlotDateViewModel> Dates { get; init; } = [];

    /// <summary>Disables the whole picker.</summary>
    public bool IsDisabled { get; init; }

    /// <summary>Explains the already-converted display times.</summary>
    public string TimeZoneLabel { get; init; } = "South Africa Standard Time (UTC+02:00)";

    /// <summary>Explanation shown when no slots can be selected.</summary>
    public string EmptyMessage { get; init; } = "No time slots are available. Please choose another date or check again later.";
}
