using Luna.Identity;
using Luna.Identity.Application;
using Luna.Identity.Infrastructure;
using Luna.Observability;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLunaOpenTelemetry(builder.Configuration, "luna-identity");
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Identity")
    .Filter.ByExcluding(logEvent => logEvent.Properties.TryGetValue("RequestPath", out var requestPath) && requestPath.ToString().Trim('"').Equals("/health", StringComparison.OrdinalIgnoreCase))
    .WriteTo.Console(), preserveStaticLogger: false, writeToProviders: true);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
    options.DocumentFilter<OpenIddictSwaggerDocumentFilter>());
builder.Services.AddIdentityInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddScoped<RegisterCustomerHandler>();
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();

var app = builder.Build();
await app.Services.MigrateAndSeedIdentityAsync();
app.UseIdentityPipeline();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

public partial class Program { }

/// <summary>
/// Test-hosting marker. The integration test project references Identity under a project alias, so the
/// global <c>Program</c> class cannot be named as <c>IdentityApi.Program</c> the way the other services are
/// referenced. This uniquely named type gives <c>WebApplicationFactory</c> something to bind to without
/// changing how the host is built.
/// </summary>
public sealed class IdentityServerEntryPoint;
