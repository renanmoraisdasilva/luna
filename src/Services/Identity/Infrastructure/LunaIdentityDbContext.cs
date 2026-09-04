using Microsoft.EntityFrameworkCore;

namespace Luna.Identity;

public sealed class LunaIdentityDbContext(DbContextOptions<LunaIdentityDbContext> options)
    : Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext<LunaUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.UseOpenIddict();
    }
}