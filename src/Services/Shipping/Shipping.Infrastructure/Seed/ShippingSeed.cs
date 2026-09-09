using Luna.Shipping.Domain;
using Microsoft.EntityFrameworkCore;

namespace Luna.Shipping.Infrastructure.Seed;

public static class ShippingSeed
{
    private static readonly (string Code, string Name, decimal Cost, int Days)[] Methods =
    [
        ("STANDARD", "Standard delivery", 5.99m, 5),
        ("EXPRESS", "Express delivery", 14.99m, 2)
    ];

    public static async Task SeedAsync(ShippingDbContext db, CancellationToken cancellationToken = default)
    {
        var existingCodes = await db.ShippingMethods
            .Select(method => method.Code)
            .ToListAsync(cancellationToken);

        var missingMethods = Methods
            .Where(method => !existingCodes.Contains(method.Code))
            .Select(method => ShippingMethod.Create(method.Code, method.Name, method.Cost, method.Days))
            .ToArray();

        if (missingMethods.Length == 0)
        {
            return;
        }

        db.ShippingMethods.AddRange(missingMethods);
        await db.SaveChangesAsync(cancellationToken);
    }
}
