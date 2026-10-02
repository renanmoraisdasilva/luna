using System.ComponentModel.DataAnnotations;

namespace Luna.Contracts.Pagination;

public sealed record PaginationRequest
{
    /// <summary>
    /// Largest page a caller may request.
    /// </summary>
    /// <remarks>
    /// Data annotations only guarantee the lower bound, so a caller can otherwise ask for
    /// <c>int.MaxValue</c> rows and make the database materialise an entire table in one response. On an
    /// anonymously reachable endpoint that is a cheap denial of service. Every paged repository applies this
    /// ceiling.
    /// </remarks>
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

    /// <summary>
    /// Rejects a page size outside the supported range.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="page"/> is below 1, or <paramref name="pageSize"/> is not between 1 and
    /// <see cref="MaxPageSize"/>.
    /// </exception>
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
