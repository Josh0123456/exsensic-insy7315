namespace Exsensic.Web.Models.Shared;

/// <summary>Paging metadata and links supplied by the parent screen.</summary>
public sealed class PagerViewModel
{
    /// <summary>One-based current page.</summary>
    public required int CurrentPage { get; init; }

    /// <summary>Total page count; use the empty state instead when zero.</summary>
    public required int TotalPages { get; init; }

    /// <summary>Optional local previous-page URL including any filters.</summary>
    public string? PreviousUrl { get; init; }

    /// <summary>Optional local next-page URL including any filters.</summary>
    public string? NextUrl { get; init; }

    /// <summary>Accessible name for this pagination landmark.</summary>
    public string Label { get; init; } = "Results pages";
}
