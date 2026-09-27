extern alias PaymentsApi;

using FluentAssertions;
using Luna.Payments.Domain;
using Luna.Payments.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Luna.IntegrationTests.Payments;

[Collection(PaymentsDatabaseCollection.Name)]
public sealed class PaymentRowVersionDiagnosticsTests(PaymentsSqlServerFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task Diagnose_row_version()
    {
        await fixture.ResetAsync();
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(fixture.ConnectionString)
            .LogTo(message => output.WriteLine($"EF: {message}"), Microsoft.Extensions.Logging.LogLevel.Information)
            .Options;
        var orderId = Guid.NewGuid();
        await using (var seedDb = new PaymentDbContext(options))
        {
            var payment = Payment.Create(orderId, 10m, "USD");
            seedDb.Payments.Add(payment);
            await seedDb.SaveChangesAsync();
        }

        await using var db = new PaymentDbContext(options);
        var payment2 = await db.Payments.SingleAsync(value => value.OrderId == orderId);
        output.WriteLine($"EF RowVersion: {Hex(payment2.RowVersion)} (len {payment2.RowVersion?.Length})");

        var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT RowVersion FROM Payments WHERE OrderId = @orderId";
            command.Parameters.AddWithValue("@orderId", orderId);
            var value = await command.ExecuteScalarAsync();
            output.WriteLine($"DB RowVersion: {Hex(value as byte[] ?? Array.Empty<byte>())} ({value?.GetType()})");

            using var typeCommand = connection.CreateCommand();
            typeCommand.CommandText = "SELECT DATA_TYPE, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Payments' AND COLUMN_NAME = 'RowVersion'";
            await using (var reader = await typeCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    output.WriteLine($"Column type: {reader.GetValue(0)}, max length: {reader.GetValue(1)}");
                }
            }
        }

        payment2.RecordAuthorizationAttempt(succeeded: true, providerReference: "prov");
        var save = () => db.SaveChangesAsync();
        var exception = await Record.ExceptionAsync(async () => await save());
        if (exception is not null)
        {
            output.WriteLine($"Update failed: {exception.GetType().Name}: {exception.Message}");
        }

        exception.Should().BeNull();
    }

    private static string Hex(byte[] bytes) => string.Join(string.Empty, bytes.Select(value => value.ToString("X2")));
}
