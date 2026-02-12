namespace Defra.Cdp.Backend.Api.Models;

public class Paginated<T>
{
    public Paginated(List<T> data, int page, int pageSize, int totalPages)
    {
        Data = data;
        Page = page;
        PageSize = pageSize;
        TotalPages = totalPages;
    }

    public List<T> Data { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }

    public void Deconstruct(out List<T> data, out int page, out int pageSize, out int totalPages)
    {
        data = Data;
        page = Page;
        pageSize = PageSize;
        totalPages = TotalPages;
    }
}

public record Pagination(int? Offset, int? Page, int? Size);
