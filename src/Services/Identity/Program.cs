using Luna.Identity;
using Luna.Identity.Application;
using Luna.Identity.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Identity")
    .WriteTo.Console());
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
    options.DocumentFilter<OpenIddictSwaggerDocumentFilter>());
builder.Services.AddIdentityInfrastructure(builder.Configuration);
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
