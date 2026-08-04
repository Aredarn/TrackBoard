using Microsoft.EntityFrameworkCore;

namespace TrackBoard.Common;

public static class QueryableExtensions
{
    /// <summary>
    /// Runs the count and the page as two queries against an already-projected
    /// <see cref="IQueryable{T}"/>, so only the requested rows leave the database.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> source,
        PageQuery page,
        CancellationToken cancellationToken = default)
    {
        var total = await source.LongCountAsync(cancellationToken);

        var items = await source
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, page.Page, page.PageSize, total);
    }
}
