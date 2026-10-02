namespace Luna.Contracts.Authentication;

public static class LunaAuthentication
{
    public const string Audience = "luna-api";
    public const string ApiResource = Audience;
    public const string SubjectClaim = "sub";
    public const string EmailClaim = "email";
    public const string NameClaim = "name";
    public const string RoleClaim = "role";
}

public static class LunaServiceClients
{
    public const string Orders = "orders";

    /// <summary>
    /// Partition key used for requests that are not rate limited. Any distinct value works; naming it keeps the
    /// intent readable at the call site.
    /// </summary>
    public const string NotLimited = "not-limited";
}

/// <summary>
/// Clients that are not confidential service identities. The storefront is a browser application and cannot
/// keep a secret, so it is registered as a public client and is limited to the password grant.
/// </summary>
public static class LunaPublicClients
{
    public const string Storefront = "luna-storefront";
}

public static class LunaServiceScopes
{
    public const string InventoryReservationsWrite = "inventory.reservations.write";
    public const string PaymentsAuthorize = "payments.authorize";
    public const string ShippingShipmentsWrite = "shipping.shipments.write";
}
