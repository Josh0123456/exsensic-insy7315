namespace Exsensic.Web.Models.Shared;

/// <summary>One time choice and its externally determined availability.</summary>
public sealed class SlotOptionViewModel
{
    /// <summary>Value submitted by the radio input.</summary>
    public required string Id { get; init; }

    /// <summary>Start time in the picker's stated display time zone.</summary>
    public required TimeOnly StartTime { get; init; }

    /// <summary>Optional end time.</summary>
    public TimeOnly? EndTime { get; init; }

    /// <summary>Whether the caller permits selecting this slot.</summary>
    public bool IsAvailable { get; init; } = true;

    /// <summary>Optional plain-text reason; defaults to Unavailable.</summary>
    public string? UnavailableReason { get; init; }
}
