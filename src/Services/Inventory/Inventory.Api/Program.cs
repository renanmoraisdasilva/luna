using Luna.Inventory.Application.Reservations;
using Luna.Inventory.Infrastructure;
using Luna.Inventory.Api.Middleware;
using Luna.Authentication;
using Luna.Authentication.ServiceAuthentication;
using Luna.Contracts.Authentication;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext().Enrich.WithProperty("Service", "Inventory").WriteTo.Console());
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
app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<InventoryExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
