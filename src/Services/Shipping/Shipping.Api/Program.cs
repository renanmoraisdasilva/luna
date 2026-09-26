using System.Security.Claims;
using Luna.Shipping.Api.Authorization;
using Luna.Shipping.Api.Middleware;
using Luna.Shipping.Application.Quotes;
using Luna.Shipping.Application.Shipments;
using Luna.Shipping.Application.ShippingMethods;
using Luna.Shipping.Infrastructure;
using Luna.Authentication;
using Luna.Authentication.ServiceAuthentication;
using Luna.Contracts.Authentication;
using Luna.Observability;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLunaOpenTelemetry(builder.Configuration, "luna-shipping");
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Shipping")
    .Filter.ByExcluding(logEvent => logEvent.Properties.TryGetValue("RequestPath", out var requestPath) && requestPath.ToString().Trim('"').Equals("/health", StringComparison.OrdinalIgnoreCase))
    .WriteTo.Console(), preserveStaticLogger: false, writeToProviders: true);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddLunaJwtValidation(builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        ShippingAuthorizationPolicies.Customer,
        policy => policy
            .AddAuthenticationSchemes(LunaAuthenticationDefaults.ValidationScheme)
            .RequireAuthenticatedUser());
    options.AddPolicy(
        LunaServicePolicies.OrdersShippingShipmentsWrite,
        policy => policy.RequireLunaService(
            LunaServiceClients.Orders,
            LunaServiceScopes.ShippingShipmentsWrite));
    options.AddPolicy(
        ShippingAuthorizationPolicies.Operations,
        policy => policy
            .AddAuthenticationSchemes(LunaAuthenticationDefaults.ValidationScheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(context => context.User.Claims.Any(claim =>
                (claim.Type == LunaAuthentication.RoleClaim || claim.Type == ClaimTypes.Role)
                && claim.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase))));
});
builder.Services.AddShippingInfrastructure(builder.Configuration);
builder.Services.AddScoped<GetShippingMethodsHandler>();
builder.Services.AddScoped<QuoteShippingHandler>();
builder.Services.AddScoped<CreateShipmentHandler>();
builder.Services.AddScoped<GetShipmentsHandler>();
builder.Services.AddScoped<GetShipmentHandler>();
builder.Services.AddScoped<GetShipmentTrackingHandler>();
builder.Services.AddScoped<MarkShipmentInTransitHandler>();
builder.Services.AddScoped<MarkShipmentDeliveredHandler>();
builder.Services.AddHealthChecks();

var app = builder.Build();
await app.Services.InitializeShippingDatabaseAsync();
app.UseSerilogRequestLogging(options => options.GetLevel = (httpContext, _, exception) => httpContext.Request.Path == "/health" ? LogEventLevel.Verbose : exception is not null ? LogEventLevel.Error : LogEventLevel.Information);
app.UseSwagger();
app.UseSwaggerUI();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ShippingExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

public partial class Program { }
