using Luna.Authentication;
using Luna.Orders.Application.Carts;
using Luna.Orders.Contracts.Carts;
using Microsoft.AspNetCore.Authorization;
using Luna.Contracts.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Luna.Orders.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = LunaAuthenticationDefaults.ValidationScheme)]
[Route("api/v1/orders/cart")]
public sealed class CartsController(
    GetCartHandler getCart,
    AddCartItemHandler addCartItem,
    ChangeCartItemQuantityHandler changeCartItemQuantity,
    RemoveCartItemHandler removeCartItem) : ControllerBase
{
    [HttpGet]
    public Task<CartResponse> Get(CancellationToken cancellationToken) =>
        getCart.HandleAsync(GetCustomerId(), cancellationToken);

    [HttpPost("items")]
    public Task<CartResponse> AddItem(AddCartItemRequest request, CancellationToken cancellationToken) =>
        addCartItem.HandleAsync(
            new AddCartItemCommand(GetCustomerId(), request.ProductId, request.Quantity),
            cancellationToken);

    [HttpPut("items/{productId:guid}")]
    public Task<CartResponse> ChangeItemQuantity(Guid productId, ChangeCartItemQuantityRequest request, CancellationToken cancellationToken) =>
        changeCartItemQuantity.HandleAsync(
            new ChangeCartItemQuantityCommand(GetCustomerId(), productId, request.Quantity),
            cancellationToken);

    [HttpDelete("items/{productId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid productId, CancellationToken cancellationToken)
    {
        await removeCartItem.HandleAsync(
            new RemoveCartItemCommand(GetCustomerId(), productId),
            cancellationToken);
        return NoContent();
    }

    private Guid GetCustomerId()
    {
        return User.TryGetSubjectId(out var customerId)
            ? customerId
            : throw new UnauthenticatedCustomerException();
    }
}
