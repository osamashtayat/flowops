using Microsoft.EntityFrameworkCore;

namespace FlowOps.Application.Common;

/// <summary>The one paging implementation (CODE_STANDARDS.md §2.3). Callers order the query deterministically first.</summary>
public static class Paging
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> ordered, PageRequest page, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(page);
        var total = await ordered.CountAsync(ct);
        var items = await ordered.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<T>(items, page.Page, page.PageSize, total);
    }

    public static PagedResult<TOut> Map<TIn, TOut>(this PagedResult<TIn> page, Func<TIn, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new PagedResult<TOut>([.. page.Items.Select(map)], page.Page, page.PageSize, page.TotalCount);
    }
}
