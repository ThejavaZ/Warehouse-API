using System.ComponentModel.DataAnnotations;

namespace Api.Common;

public class PagedResult<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int TotalItems { get; set; }
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
    public int? PreviousPage => HasPreviousPage ? Page - 1 : null;
    public int? NextPage => HasNextPage ? Page + 1 : null;
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
}
