using System.ComponentModel.DataAnnotations;

namespace Luna.Contracts.Pagination;

public sealed record PaginationRequest(
	[property: Range(1, int.MaxValue)] int Page = 1,
	[property: Range(1, int.MaxValue)] int PageSize = 20);
