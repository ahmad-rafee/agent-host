using AgentHost.Api.Composition;
using AgentHost.Api.Endpoints;
using AgentHost.Shared.Observability;
using AgentHost.Shared.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddSwaggerGen();
}

// Configure JSON serialization for .NET 9 compatibility
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = false;
    // Disable streaming to work around PipeWriter.UnflushedBytes issue
    options.SerializerOptions.DefaultBufferSize = 16384;
});

builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddObservability("AgentHost.Api");

// Configure logging
builder.Logging.AddStructuredLogging();

var app = builder.Build();

// Run database migrations on startup
try
{
    var connectionString = builder.Configuration.GetConnectionString("Db") 
        ?? throw new InvalidOperationException("Database connection string is required");
    
    DatabaseMigrator.MigrateDatabase(connectionString, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Failed to migrate database");
    throw;
}

// Configure pipeline
app.ConfigureApiPipeline();

// In testing environment, skip Swagger UI middleware to reduce surface of response writers
if (app.Environment.IsEnvironment("Testing"))
{
    // Remove Swagger related endpoints if registered
    // (No explicit removal API; conditional registration above prevents adding.)
}

// Map endpoints
app.MapHealthEndpoints();
app.MapRunEndpoints();
app.MapArtifactEndpoints();

app.Logger.LogInformation("AgentHost API starting on {Urls}", string.Join(", ", app.Urls));

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
