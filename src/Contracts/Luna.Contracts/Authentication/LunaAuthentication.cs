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
}

public static class LunaServiceScopes
{
    public const string InventoryReservationsWrite = "inventory.reservations.write";
    public const string PaymentsAuthorize = "payments.authorize";
    public const string ShippingShipmentsWrite = "shipping.shipments.write";
}
