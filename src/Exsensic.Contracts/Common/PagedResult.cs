namespace Exsensic.Contracts.Common;

/// <summary>
/// One page of a longer list, returned by every list endpoint that pages (docs/CONTRACTS.md §5–6).
/// Pages count from 1; the API uses a page size of 20 by default and at most 100.
/// </summary>
/// <typeparam name="T">The item DTO, for example BookingSummaryDto.</typeparam>
/// <param name="Items">The items on this page, already sorted by the API.</param>
/// <param name="Page">The page number, starting at 1.</param>
/// <param name="PageSize">The maximum number of items on a page.</param>
/// <param name="TotalCount">The number of matching items across all pages.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    /// <summary>
    /// How many pages there are in total, so the pager can show "Page X of Y".
    /// Returns 0 when there are no items (or the page size is invalid) instead of dividing by zero.
    /// </summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
