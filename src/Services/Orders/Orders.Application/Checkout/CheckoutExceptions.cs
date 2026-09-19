namespace Luna.Orders.Application.Checkout;

public sealed class CheckoutRejectedException(string code, string message, Exception? innerException = null) : Exception(message, innerException)
{
    public string Code { get; } = code;
}
