namespace Exsensic.Web.Models.Shared;

/// <summary>A caller-supplied date and its time choices.</summary>
public sealed class SlotDateViewModel
{
    /// <summary>Date represented by this option.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Whether the caller permits selecting this date.</summary>
    public bool IsAvailable { get; init; } = true;

    /// <summary>Time choices supplied for this date.</summary>
    public IReadOnlyList<SlotOptionViewModel> Slots { get; init; } = [];
}
