using Luna.Shipping.Api.Middleware;
using Luna.Shipping.Application.Quotes;
using Luna.Shipping.Application.Shipments;
using Luna.Shipping.Application.ShippingMethods;
using Luna.Shipping.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Shipping")
    .WriteTo.Console());
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddShippingInfrastructure(builder.Configuration);
builder.Services.AddScoped<GetShippingMethodsHandler>();
builder.Services.AddScoped<QuoteShippingHandler>();
builder.Services.AddScoped<CreateShipmentHandler>();
builder.Services.AddHealthChecks();

var app = builder.Build();
await app.Services.InitializeShippingDatabaseAsync();
app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ShippingExceptionHandlingMiddleware>();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

public partial class Program { }
