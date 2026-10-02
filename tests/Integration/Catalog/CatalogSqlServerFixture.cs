using Luna.Catalog.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Respawn;
using Testcontainers.MsSql;
using Xunit;

namespace Luna.IntegrationTests.Catalog;

public sealed class CatalogSqlServerFixture : IAsyncLifetime
{
    private const string Password = "Your_password123";
    private const string DatabaseName = "CatalogTests";
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

    public CatalogDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new CatalogDbContext(options);
    }

    public string ConnectionString => new SqlConnectionStringBuilder(container.GetConnectionString())
    {
        InitialCatalog = DatabaseName
    }.ConnectionString;

    public async Task DisposeAsync()
    {
        await container.DisposeAsync();
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CatalogDatabaseCollection : ICollectionFixture<CatalogSqlServerFixture>
{
    public const string Name = "Catalog database";
}
