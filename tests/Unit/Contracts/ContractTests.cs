using FluentAssertions;
using System.ComponentModel.DataAnnotations;
using Luna.Contracts.Correlation;
using Luna.Contracts.Errors;
using Luna.Contracts.Pagination;
using Xunit;

namespace Luna.UnitTests.Contracts;

public sealed class ContractTests
{
    [Fact]
    public void ApiError_preserves_common_error_shape()
    {
        var error = new ApiError("PRODUCT_NOT_FOUND", "Product was not found.");

        error.Code.Should().Be("PRODUCT_NOT_FOUND");
        error.Message.Should().Be("Product was not found.");
    }

    [Fact]
    public void PaginationRequest_uses_default_values()
    {
        var request = new PaginationRequest();

        request.Page.Should().Be(1);
        request.PageSize.Should().Be(20);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    public void PaginationRequest_rejects_values_below_one(int page, int pageSize)
    {
        var request = new PaginationRequest(page, pageSize);
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true)
            .Should().BeFalse();
    }

    [Fact]
    public void PaginationRequest_allows_service_specific_page_sizes()
    {
        var request = new PaginationRequest(Page: 1, PageSize: 500);
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true)
            .Should().BeTrue();
    }

    [Fact]
    public void PaginatedResponse_preserves_items_and_metadata()
    {
        var response = new PaginatedResponse<string>(["Keyboard", "Mouse"], 2, 2, 5);

        response.Items.Should().Equal("Keyboard", "Mouse");
        response.Page.Should().Be(2);
        response.PageSize.Should().Be(2);
        response.TotalCount.Should().Be(5);
    }

    [Fact]
    public void CorrelationHeaders_defines_the_cross_service_header()
    {
        CorrelationHeaders.CorrelationId.Should().Be("X-Correlation-ID");
    }
}
