using Luna.Authentication;
using Luna.Catalog.Infrastructure;
using Luna.Catalog.Api.Middleware;
using Luna.Observability;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLunaOpenTelemetry(builder.Configuration, "luna-catalog");
builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext().Enrich.WithProperty("Service", "Catalog").Filter.ByExcluding(logEvent => logEvent.Properties.TryGetValue("RequestPath", out var requestPath) && requestPath.ToString().Trim('"').Equals("/health", StringComparison.OrdinalIgnoreCase)).WriteTo.Console(), preserveStaticLogger: false, writeToProviders: true);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddLunaJwtValidation(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddCatalogInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks();
var app = builder.Build();
await app.Services.InitializeCatalogDatabaseAsync();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging(options => options.GetLevel = (httpContext, _, exception) => httpContext.Request.Path == "/health" ? LogEventLevel.Verbose : exception is not null ? LogEventLevel.Error : LogEventLevel.Information);
if (app.Environment.IsDevelopment())
{
    // The OpenAPI document is development-only. Publishing it hands anonymous callers the full endpoint
    // surface of the service, including the scopes its internal endpoints require.
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
public partial class Program { }
