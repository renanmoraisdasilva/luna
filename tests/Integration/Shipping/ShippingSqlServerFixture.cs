using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Respawn;
using Testcontainers.MsSql;
using Xunit;
using Luna.Shipping.Infrastructure;

namespace Luna.IntegrationTests.Shipping;

public sealed class ShippingSqlServerFixture : IAsyncLifetime
{
    private const string Password = "Your_password123";
    private const string DatabaseName = "ShippingTests";
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword(Password)
        .Build();
    private Respawner respawner = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
        respawner = await Respawner.CreateAsync(ConnectionString, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            TablesToIgnore = ["__EFMigrationsHistory"]
        });
    }

    public Task ResetAsync() => respawner.ResetAsync(ConnectionString);

    public ShippingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ShippingDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new ShippingDbContext(options);
    }

    public string ConnectionString => new SqlConnectionStringBuilder(container.GetConnectionString())
    {
        InitialCatalog = DatabaseName
    }.ConnectionString;

    public async Task DisposeAsync() => await container.DisposeAsync();
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ShippingDatabaseCollection : ICollectionFixture<ShippingSqlServerFixture>
{
    public const string Name = "Shipping database";
}
