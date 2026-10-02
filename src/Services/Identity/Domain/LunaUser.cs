using Microsoft.AspNetCore.Identity;

namespace Luna.Identity;

public sealed class LunaUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
