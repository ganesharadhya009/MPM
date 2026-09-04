namespace PeopleHQ.Application.Common;

public record PagedMeta(int Page, int PageSize, int TotalItems, int TotalPages);

public record PagedResult<T>(IReadOnlyList<T> Data, PagedMeta Meta)
{
    public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalItems) =>
        new(items, new PagedMeta(page, pageSize, totalItems, pageSize <= 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize)));
}
