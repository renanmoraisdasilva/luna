using Luna.Payments.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Respawn;
using Testcontainers.MsSql;
using Xunit;

namespace Luna.IntegrationTests.Payments;

public sealed class PaymentsSqlServerFixture : IAsyncLifetime
{
    private const string Password = "Your_password123";
    private const string DatabaseName = "PaymentsTests";
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

    public PaymentDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new PaymentDbContext(options);
    }

    public string ConnectionString => new SqlConnectionStringBuilder(container.GetConnectionString())
    {
        InitialCatalog = DatabaseName
    }.ConnectionString;

    public Task DisposeAsync() => container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PaymentsDatabaseCollection : ICollectionFixture<PaymentsSqlServerFixture>
{
    public const string Name = "Payments database";
}
