using Luna.Contracts.Authentication;

namespace Luna.Contracts.Errors;

/// <summary>
/// Raised when a request carries no usable customer identity.
/// </summary>
/// <remarks>
/// This is an authentication failure, not a business-rule violation, so it must surface as 401 rather than
/// 404 or 409. Previously these five sites threw <see cref="InvalidOperationException"/>, which the Orders and
/// Shipping exception middleware mapped to <c>409 FULFILLMENT_CONFLICT</c> and <c>409 SHIPPING_CONFLICT</c>
/// respectively — telling a client that its request conflicted with server state when in fact nobody was
/// authenticated. A distinct type lets the middleware map the intent instead of the exception base class.
/// </remarks>
public sealed class UnauthenticatedCustomerException()
    : Exception("The request is not associated with an authenticated customer.")
{
    /// <summary>Machine-readable error code returned to the client.</summary>
    public const string ErrorCode = "UNAUTHENTICATED";
}
