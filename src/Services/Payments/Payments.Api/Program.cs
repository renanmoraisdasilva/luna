using Luna.Payments.Api.Middleware;
using Luna.Payments.Application.Authorization;
using Luna.Payments.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Payments")
    .WriteTo.Console());
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddPaymentsInfrastructure(builder.Configuration);
builder.Services.AddScoped<AuthorizePaymentHandler>();
builder.Services.AddHealthChecks();

var app = builder.Build();
await app.Services.InitializePaymentsDatabaseAsync();
app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<PaymentsExceptionHandlingMiddleware>();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

public partial class Program { }
