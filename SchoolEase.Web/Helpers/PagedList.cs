namespace SchoolEase.Web.Helpers;

/// <summary>
/// Minimal server-side paging wrapper that also carries the query state needed
/// to rebuild filter/sort links in the UI.
/// </summary>
public class PagedList<T> : List<T>
{
    public int PageIndex { get; private set; }
    public int PageSize { get; private set; }
    public int TotalCount { get; private set; }

    public int TotalPages => PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => PageIndex > 1;
    public bool HasNext => PageIndex < TotalPages;
    public int FirstItemOnPage => TotalCount == 0 ? 0 : ((PageIndex - 1) * PageSize) + 1;
    public int LastItemOnPage => Math.Min(PageIndex * PageSize, TotalCount);

    public PagedList(List<T> items, int totalCount, int pageIndex, int pageSize)
    {
        TotalCount = totalCount;
        PageIndex = pageIndex;
        PageSize = pageSize;
        AddRange(items);
    }

    public static async Task<PagedList<T>> CreateAsync(IQueryable<T> source, int pageIndex, int pageSize)
    {
        pageIndex = pageIndex < 1 ? 1 : pageIndex;
        pageSize = pageSize is < 1 or > 200 ? 10 : pageSize;

        var total = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(source);
        var items = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            source.Skip((pageIndex - 1) * pageSize).Take(pageSize));

        return new PagedList<T>(items, total, pageIndex, pageSize);
    }
}
