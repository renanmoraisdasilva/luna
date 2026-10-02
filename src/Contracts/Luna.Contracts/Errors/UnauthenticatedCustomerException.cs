using Luna.Contracts.Authentication;

namespace Luna.Contracts.Errors;

public sealed class UnauthenticatedCustomerException()
    : Exception("The request is not associated with an authenticated customer.")
{
    public const string ErrorCode = "UNAUTHENTICATED";
}
