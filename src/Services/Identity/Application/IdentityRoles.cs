namespace Luna.Identity.Application;

public static class IdentityRoles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";

    public static readonly IReadOnlyCollection<string> All = [Admin, Customer];
}