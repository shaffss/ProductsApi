using Products.Api;
using Products.Api.Contracts;
using Products.Api.DevLauncher;
using Products.Api.Endpoints;
using Products.Core;
using Products.Persistence;
using Products.Persistence.EfCore;

args = await DevLauncher.ApplyAsync(args); // profil "Launcher": okno wyboru wariantu (bez --launcher nic nie robi)

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(); // błędy (400/404/500) w formacie RFC 9457
builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true); // jednakowo w każdym środowisku
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonContext.Default));
builder.Services.AddOpenApi(o => o.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "Products API";
    document.Info.Description = "Products with Redis cache (cache-aside) and Kafka events on price change.";
    return Task.CompletedTask;
}));
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddCaching(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration);
builder.Services.AddSingleton<ProductService>();
builder.AddObservability();

var app = builder.Build();
var persistence = app.Services.GetRequiredService<PersistenceOptions>();
LogVariant(app.Logger, persistence.Variant);

// Database:MigrateOnStartup (włączone w docker-compose.yml) - schemat z migracji EF przy starcie,
// zamiast ręcznego dotnet ef database update
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    var connectionString = app.Configuration.GetConnectionString(persistence.Database.ToString())!;
    await DatabaseMigrator.MigrateAsync(persistence.Database, connectionString, app.Lifetime.ApplicationStopping);
    LogMigrated(app.Logger);
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // /openapi/v1.json
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Products API")); // /swagger
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.MapProductEndpoints();
app.MapHealthEndpoints();

app.Run();

// public: dostęp dla WebApplicationFactory w testach integracyjnych
public partial class Program
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Persistence: {Variant}")]
    private static partial void LogVariant(ILogger logger, string variant);

    [LoggerMessage(Level = LogLevel.Information, Message = "Database migrated")]
    private static partial void LogMigrated(ILogger logger);
}
