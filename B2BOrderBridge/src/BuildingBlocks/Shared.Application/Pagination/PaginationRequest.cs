namespace Shared.Application.Pagination;

/// <summary>One-based page request. Apply a stable ordering before Skip/Take.</summary>
public sealed record PaginationRequest
{
    public const int MaxPageSize = 100;

    public PaginationRequest(int pageNumber = 1, int pageSize = 20)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaxPageSize);
        if ((long)(pageNumber - 1) * pageSize > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page offset is too large.");

        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    public int PageNumber { get; }
    public int PageSize { get; }
    public int Offset => (PageNumber - 1) * PageSize;
}
