namespace Shared.Application.Pagination;

/// <summary>A page of data, not an operation outcome. Wrap in Result when needed.</summary>
public sealed class PaginationResult<T>
{
    public PaginationResult(IEnumerable<T> items, long totalCount, PaginationRequest pagination)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(pagination);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);

        var snapshot = items.ToArray();
        var available = Math.Max(0L, totalCount - pagination.Offset);
        if (snapshot.Length > pagination.PageSize || snapshot.LongLength > available)
            throw new ArgumentException("Items exceed the page size or available total.", nameof(items));

        Items = Array.AsReadOnly(snapshot);
        TotalCount = totalCount;
        PageNumber = pagination.PageNumber;
        PageSize = pagination.PageSize;
    }

    public IReadOnlyList<T> Items { get; }
    public long TotalCount { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public long TotalPages => TotalCount / PageSize + (TotalCount % PageSize == 0 ? 0 : 1);
    public bool HasPreviousPage => PageNumber > 1 && TotalCount > 0;
    public bool HasNextPage => PageNumber < TotalPages;
}
