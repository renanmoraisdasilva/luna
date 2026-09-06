using Luna.Authentication;
using Luna.Orders.Application.Carts;
using Luna.Orders.Contracts.Carts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luna.Orders.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = LunaAuthenticationDefaults.ValidationScheme)]
[Route("api/v1/orders/cart")]
public sealed class CartsController(CartService cartService) : ControllerBase
{
    [HttpGet]
    public Task<CartResponse> Get(CancellationToken cancellationToken) =>
        cartService.GetAsync(GetCustomerId(), cancellationToken);

    [HttpPost("items")]
    public Task<CartResponse> AddItem(AddCartItemRequest request, CancellationToken cancellationToken) =>
        cartService.AddItemAsync(GetCustomerId(), request.ProductId, request.Quantity, cancellationToken);

    [HttpPut("items/{productId:guid}")]
    public Task<CartResponse> ChangeItemQuantity(Guid productId, ChangeCartItemQuantityRequest request, CancellationToken cancellationToken) =>
        cartService.ChangeItemQuantityAsync(GetCustomerId(), productId, request.Quantity, cancellationToken);

    [HttpDelete("items/{productId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid productId, CancellationToken cancellationToken)
    {
        await cartService.RemoveItemAsync(GetCustomerId(), productId, cancellationToken);
        return NoContent();
    }

    private Guid GetCustomerId()
    {
        return User.TryGetSubjectId(out var customerId)
            ? customerId
            : throw new InvalidOperationException("The authenticated customer ID is missing or invalid.");
    }
}
