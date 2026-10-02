using System.Security.Claims;
using FluentAssertions;
using Luna.Contracts.Errors;
using Luna.Orders.Api.Controllers;
using Luna.Orders.Application.Carts;
using Luna.Orders.Contracts.Carts;
using Luna.Orders.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Luna.IntegrationTests.Orders;

public sealed class CartsControllerUnitTests
{
    [Fact]
    public async Task Rejects_a_user_without_a_valid_customer_subject_as_unauthenticated()
    {
        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity()),
            },
        };

        var act = () => controller.Get(CancellationToken.None);

        // A dedicated exception type rather than InvalidOperationException, so the middleware reports 401
        // instead of a 409 that would read as a conflict with server state.
        await act.Should().ThrowAsync<UnauthenticatedCustomerException>();
    }

    private static CartsController CreateController() => new(
        new GetCartHandler(new EmptyReadRepository()),
        new AddCartItemHandler(new EmptyWriteRepository()),
        new ChangeCartItemQuantityHandler(new EmptyWriteRepository()),
        new RemoveCartItemHandler(new EmptyWriteRepository()));

    private sealed class EmptyReadRepository : ICartReadRepository
    {
        public Task<CartResponse?> GetCartAsync(Guid customerId, CancellationToken cancellationToken) =>
            Task.FromResult<CartResponse?>(null);
    }

    private sealed class EmptyWriteRepository : ICartWriteRepository
    {
        public Task<Cart?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken) =>
            Task.FromResult<Cart?>(null);

        public Task<Cart> GetOrCreateAsync(Guid customerId, CancellationToken cancellationToken) =>
            Task.FromResult(Cart.Create(customerId));

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
