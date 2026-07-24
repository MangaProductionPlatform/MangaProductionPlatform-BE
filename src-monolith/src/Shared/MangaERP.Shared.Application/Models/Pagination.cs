namespace MangaERP.Shared.Application.Models;

public record PagedRequest(
    int PageIndex = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    string? SortBy = null,
    bool SortDescending = false
)
{
    public int NormalizedPageIndex => Math.Max(1, PageIndex);
    public int NormalizedPageSize => Math.Clamp(PageSize, 1, 100);
}

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageIndex,
    int PageSize
)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;
}
