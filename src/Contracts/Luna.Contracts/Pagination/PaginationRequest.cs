using System.ComponentModel.DataAnnotations;

namespace Luna.Contracts.Pagination;

public sealed record PaginationRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, int.MaxValue)]
    public int PageSize { get; init; } = 20;

    public PaginationRequest()
    {
    }

    public PaginationRequest(int Page = 1, int PageSize = 20)
    {
        this.Page = Page;
        this.PageSize = PageSize;
    }
}
