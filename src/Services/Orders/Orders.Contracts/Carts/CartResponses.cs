namespace Luna.Orders.Contracts.Carts;

public sealed record CartResponse(Guid Id, Guid CustomerId, IReadOnlyCollection<CartItemResponse> Items);

public sealed record CartItemResponse(Guid ProductId, int Quantity);

public sealed record AddCartItemRequest(Guid ProductId, int Quantity);

public sealed record ChangeCartItemQuantityRequest(int Quantity);
