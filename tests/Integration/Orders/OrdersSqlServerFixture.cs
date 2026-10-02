using Luna.Orders.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Respawn;
using Testcontainers.MsSql;
using Xunit;

namespace Luna.IntegrationTests.Orders;

public sealed class OrdersSqlServerFixture : IAsyncLifetime
{
    private const string Password = "Your_password123";
    private const string DatabaseName = "OrdersTests";
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

    public async Task ResetAsync() => await respawner.ResetAsync(ConnectionString);

    public OrdersDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new OrdersDbContext(options);
    }

    public string ConnectionString => new SqlConnectionStringBuilder(container.GetConnectionString())
    {
        InitialCatalog = DatabaseName
    }.ConnectionString;

    public async Task DisposeAsync() => await container.DisposeAsync();
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OrdersDatabaseCollection : ICollectionFixture<OrdersSqlServerFixture>
{
    public const string Name = "Orders database";
}
