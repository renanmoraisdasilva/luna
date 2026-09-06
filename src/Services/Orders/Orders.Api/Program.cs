using Luna.Authentication;
using Luna.Orders.Api.Middleware;
using Luna.Orders.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext().Enrich.WithProperty("Service", "Orders").WriteTo.Console());
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddLunaJwtValidation(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddOrdersInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();
await app.Services.InitializeOrdersDatabaseAsync();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

public partial class Program { }
