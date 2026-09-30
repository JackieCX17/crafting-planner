namespace CraftingPlanner.Api.Contracts;

/// <summary>
/// One page of a longer list, with enough information to ask for the other pages.
/// </summary>
/// <typeparam name="T">The kind of record in the list.</typeparam>
public class PagedResult<T>
{
    /// <summary>The records on this page.</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>The page number that was returned. The first page is 1.</summary>
    public required int Page { get; init; }

    /// <summary>The largest number of records a page can hold.</summary>
    public required int PageSize { get; init; }

    /// <summary>How many records matched across every page.</summary>
    public required int TotalCount { get; init; }

    /// <summary>How many pages there are. 0 when nothing matched.</summary>
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
