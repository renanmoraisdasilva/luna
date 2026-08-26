using Luna.Catalog.Infrastructure;
using Luna.Catalog.Infrastructure.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext().Enrich.WithProperty("Service", "Catalog").WriteTo.Console());
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCatalogInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks();
var app = builder.Build();
await app.Services.InitializeCatalogDatabaseAsync();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
public partial class Program { }
