using Luna.Authentication;
using Luna.Contracts.Authentication;
using Luna.Orders.Api.Authorization;
using Luna.Orders.Api.Middleware;
using Luna.Orders.Application.Orders;
using Luna.Orders.Infrastructure;
using Luna.Observability;
using Serilog;
using Serilog.Events;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLunaOpenTelemetry(builder.Configuration, "luna-orders");
builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext().Enrich.WithProperty("Service", "Orders").Filter.ByExcluding(logEvent => logEvent.Properties.TryGetValue("RequestPath", out var requestPath) && requestPath.ToString().Trim('"').Equals("/health", StringComparison.OrdinalIgnoreCase)).WriteTo.Console(), preserveStaticLogger: false, writeToProviders: true);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddLunaJwtValidation(builder.Configuration);
builder.Services.AddAuthorization(options => options.AddPolicy(
	OrdersAuthorizationPolicies.Operations,
	policy => policy
		.AddAuthenticationSchemes(LunaAuthenticationDefaults.ValidationScheme)
		.RequireAuthenticatedUser()
		.RequireAssertion(context => context.User.Claims.Any(claim =>
			(claim.Type == LunaAuthentication.RoleClaim || claim.Type == ClaimTypes.Role)
			&& claim.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase)))));
builder.Services.AddOrdersInfrastructure(builder.Configuration);
builder.Services.AddScoped<GetFulfillmentQueueHandler>();
builder.Services.AddScoped<GetFulfillmentOrderHandler>();
builder.Services.AddScoped<PrepareFulfillmentOrderHandler>();
builder.Services.AddScoped<CreateShipmentHandler>();
builder.Services.AddScoped<MarkShipmentInTransitHandler>();
builder.Services.AddScoped<MarkShipmentDeliveredHandler>();
builder.Services.AddHealthChecks();

var app = builder.Build();
await app.Services.InitializeOrdersDatabaseAsync();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging(options => options.GetLevel = (httpContext, _, exception) => httpContext.Request.Path == "/health" ? LogEventLevel.Verbose : exception is not null ? LogEventLevel.Error : LogEventLevel.Information);
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

public partial class Program { }
