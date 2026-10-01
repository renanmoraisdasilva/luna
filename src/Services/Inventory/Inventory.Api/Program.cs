using Luna.Inventory.Application.Reservations;
using Luna.Inventory.Infrastructure;
using Luna.Inventory.Api.Middleware;
using Luna.Authentication;
using Luna.Authentication.ServiceAuthentication;
using Luna.Contracts.Authentication;
using Luna.Observability;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLunaOpenTelemetry(builder.Configuration, "luna-inventory");
builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext().Enrich.WithProperty("Service", "Inventory").Filter.ByExcluding(logEvent => logEvent.Properties.TryGetValue("RequestPath", out var requestPath) && requestPath.ToString().Trim('"').Equals("/health", StringComparison.OrdinalIgnoreCase)).WriteTo.Console(), preserveStaticLogger: false, writeToProviders: true);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddLunaJwtValidation(builder.Configuration);
builder.Services.AddAuthorization(options => options.AddPolicy(
	LunaServicePolicies.OrdersInventoryReservationsWrite,
	policy => policy.RequireLunaService(
		LunaServiceClients.Orders,
		LunaServiceScopes.InventoryReservationsWrite)));
builder.Services.AddInventoryInfrastructure(builder.Configuration);
builder.Services.AddScoped<ReserveInventoryHandler>();
builder.Services.AddScoped<ReleaseReservationHandler>();
builder.Services.AddHealthChecks();
var app = builder.Build();
await app.Services.InitializeInventoryDatabaseAsync();
app.UseSerilogRequestLogging(options => options.GetLevel = (httpContext, _, exception) => httpContext.Request.Path == "/health" ? LogEventLevel.Verbose : exception is not null ? LogEventLevel.Error : LogEventLevel.Information);
if (app.Environment.IsDevelopment())
{
    // The OpenAPI document is development-only. Publishing it hands anonymous callers the full endpoint
    // surface of the service, including the scopes its internal endpoints require.
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<InventoryExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

public partial class Program { }
