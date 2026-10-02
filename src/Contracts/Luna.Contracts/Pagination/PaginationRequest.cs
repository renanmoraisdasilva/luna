using System.ComponentModel.DataAnnotations;

namespace Luna.Contracts.Pagination;

public sealed record PaginationRequest
{
    public const int MaxPageSize = 100;

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

    public void Validate()
    {
        if (Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Page), "Page must be greater than zero.");
        }

        if (PageSize is < 1 or > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PageSize),
                $"Page size must be between 1 and {MaxPageSize}.");
        }
    }
}
