using Luna.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace Luna.Inventory.Infrastructure.Seed;

public static class InventorySeed
{
    private const int DefaultQuantityOnHand = 100;

    private static readonly Guid[] CatalogProductIds =
    [
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
        Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
        Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
        Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
        Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
        Guid.Parse("10101010-1010-1010-1010-101010101010"),
        Guid.Parse("12121212-1212-1212-1212-121212121212"),
        Guid.Parse("13131313-1313-1313-1313-131313131313"),
        Guid.Parse("14141414-1414-1414-1414-141414141414"),
        Guid.Parse("15151515-1515-1515-1515-151515151515"),
        Guid.Parse("16161616-1616-1616-1616-161616161616"),
        Guid.Parse("17171717-1717-1717-1717-171717171717"),
        Guid.Parse("18181818-1818-1818-1818-181818181818"),
        Guid.Parse("19191919-1919-1919-1919-191919191919"),
        Guid.Parse("20202020-2020-2020-2020-202020202020"),
        Guid.Parse("21212121-2121-2121-2121-212121212121"),
        Guid.Parse("23232323-2323-2323-2323-232323232323"),
        Guid.Parse("24242424-2424-2424-2424-242424242424"),
        Guid.Parse("25252525-2525-2525-2525-252525252525"),
        Guid.Parse("26262626-2626-2626-2626-262626262626"),
        Guid.Parse("27272727-2727-2727-2727-272727272727")
    ];

    public static async Task SeedAsync(
        InventoryDbContext db,
        CancellationToken cancellationToken = default)
    {
        var existingProductIds = await db.Stocks
            .Select(stock => stock.ProductId)
            .ToListAsync(cancellationToken);

        var missingStocks = CatalogProductIds
            .Where(productId => !existingProductIds.Contains(productId))
            .Select(productId => Stock.Create(productId, DefaultQuantityOnHand))
            .ToArray();

        if (missingStocks.Length == 0)
        {
            return;
        }

        db.Stocks.AddRange(missingStocks);
        await db.SaveChangesAsync(cancellationToken);
    }
}
