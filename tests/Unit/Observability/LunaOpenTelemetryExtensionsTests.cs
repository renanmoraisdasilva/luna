using FluentAssertions;
using Luna.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Luna.UnitTests.Observability;

public sealed class LunaOpenTelemetryExtensionsTests
{
    [Fact]
    public async Task Host_starts_when_otlp_endpoint_is_unavailable()
    {
        using var host = CreateHost(enabled: true, endpoint: "http://127.0.0.1:1");

        var start = () => host.StartAsync();

        await start.Should().NotThrowAsync();

        var log = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("TelemetryTest");
        var writeLog = () => log.LogInformation("Telemetry export failure must not affect application logging");

        writeLog.Should().NotThrow();
        await host.StopAsync();
    }

    [Fact]
    public async Task Telemetry_can_be_disabled_without_removing_host_startup()
    {
        using var host = CreateHost(enabled: false, endpoint: "http://127.0.0.1:1");

        var start = () => host.StartAsync();

        await start.Should().NotThrowAsync();
        await host.StopAsync();
    }

    private static IHost CreateHost(bool enabled, string endpoint)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Telemetry:Enabled"] = enabled.ToString(),
                ["Telemetry:OtlpEndpoint"] = endpoint,
                ["Telemetry:Environment"] = "Test",
            })
            .Build();

        return Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddLunaOpenTelemetry(configuration, "luna-test"))
            .Build();
    }
}
