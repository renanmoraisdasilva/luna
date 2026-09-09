namespace Luna.Orders.Application.Checkout;

public sealed class CheckoutRejectedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
