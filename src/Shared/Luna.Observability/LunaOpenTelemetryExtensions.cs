using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Luna.Observability;

public static class LunaOpenTelemetryExtensions
{
    public static IServiceCollection AddLunaOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        if (!configuration.GetValue("Telemetry:Enabled", true)
            || configuration.GetValue("OTEL_SDK_DISABLED", false))
        {
            return services;
        }

        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        serviceName = configuration["Telemetry:ServiceName"]
            ?? configuration["OTEL_SERVICE_NAME"]
            ?? serviceName;

        var serviceVersion = configuration["Telemetry:ServiceVersion"]
            ?? typeof(LunaOpenTelemetryExtensions).Assembly.GetName().Version?.ToString()
            ?? "unknown";
        var environment = configuration["Telemetry:Environment"]
            ?? configuration["DOTNET_ENVIRONMENT"]
            ?? configuration["ASPNETCORE_ENVIRONMENT"]
            ?? "Development";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = environment,
                }))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options => options.RecordException = true)
                .AddHttpClientInstrumentation(options => options.RecordException = true)
                .AddSqlClientInstrumentation(options => options.RecordException = true)
                .AddOtlpExporter(CreateExporterOptions(configuration)))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(CreateExporterOptions(configuration)))
            .WithLogging(
                logging => logging.AddOtlpExporter(CreateExporterOptions(configuration)),
                options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;
                    options.ParseStateValues = true;
                });

        return services;
    }

    private static Action<OtlpExporterOptions> CreateExporterOptions(IConfiguration configuration) => options =>
    {
        var endpoint = configuration["Telemetry:OtlpEndpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        }

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        {
            options.Endpoint = endpointUri;
        }
    };
}
