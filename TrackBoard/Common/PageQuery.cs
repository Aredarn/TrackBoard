using System.ComponentModel.DataAnnotations;

namespace TrackBoard.Common;

/// <summary>
/// Query-string paging parameters. ASP.NET Core has no equivalent of Spring's
/// <c>Pageable</c>, so list endpoints bind this explicitly.
/// </summary>
public class PageQuery
{
    public const int MaxPageSize = 100;

    private int _pageSize = 20;

    [Range(1, int.MaxValue, ErrorMessage = "page must be 1 or greater.")]
    public int Page { get; set; } = 1;

    /// <summary>Capped at <see cref="MaxPageSize"/> so a caller cannot request the whole table.</summary>
    [Range(1, MaxPageSize)]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
    }

    public int Skip => (Page - 1) * PageSize;
}
