using FluentAssertions;
using Luna.Shipping.Application.Quotes;
using Luna.Shipping.Application.ShippingMethods;
using Luna.Shipping.Domain;
using Xunit;

namespace Luna.UnitTests.Shipping;

public sealed class ShippingHandlerTests
{
    [Fact]
    public async Task Quote_handler_persists_a_quote_using_the_selected_method()
    {
        var methods = new FakeMethodRepository(new ShippingMethodReadModel(Guid.NewGuid(), "STANDARD", "Standard", 5.99m, 5));
        var quotes = new FakeQuoteRepository();
        var handler = new QuoteShippingHandler(methods, quotes);

        var response = await handler.HandleAsync(
            new QuoteShippingCommand(Guid.NewGuid(), "STANDARD", "US", "90210"),
            CancellationToken.None);

        response.Cost.Should().Be(5.99m);
        quotes.Quote.Should().NotBeNull();
        quotes.Quote!.ShippingMethodCode.Should().Be("STANDARD");
    }

    [Fact]
    public async Task Quote_handler_rejects_an_empty_order_id()
    {
        var methods = new FakeMethodRepository(new ShippingMethodReadModel(Guid.NewGuid(), "STANDARD", "Standard", 5.99m, 5));
        var handler = new QuoteShippingHandler(methods, new FakeQuoteRepository());

        var act = () => handler.HandleAsync(
            new QuoteShippingCommand(Guid.Empty, "STANDARD", "US", "90210"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private sealed class FakeMethodRepository(ShippingMethodReadModel method) : IShippingMethodReadRepository
    {
        public Task<IReadOnlyCollection<ShippingMethodReadModel>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<ShippingMethodReadModel>>([method]);

        public Task<ShippingMethodReadModel> GetByCodeAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(method);
    }

    private sealed class FakeQuoteRepository : IShippingQuoteWriteRepository
    {
        public ShippingQuote? Quote { get; private set; }

        public Task AddAsync(ShippingQuote quote, CancellationToken cancellationToken)
        {
            Quote = quote;
            return Task.CompletedTask;
        }
    }
}
