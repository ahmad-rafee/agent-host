using System.Reflection;
using DbUp;
using DbUp.Postgresql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentHost.Shared.Persistence;

public static class DatabaseMigrator
{
    public static void MigrateDatabase(string connectionString, ILogger? logger = null)
    {
        logger?.LogInformation("Starting database migration...");

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            logger?.LogError("Database migration failed: {Error}", result.Error);
            throw new InvalidOperationException($"Database migration failed: {result.Error}");
        }

        logger?.LogInformation("Database migration completed successfully");
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Db") 
            ?? throw new InvalidOperationException("Database connection string is required");

        services.AddScoped<IRunRepository>(_ => new RunRepository(connectionString));
        services.AddScoped<IStepRepository>(_ => new StepRepository(connectionString));
        services.AddScoped<IArtifactRepository>(_ => new ArtifactRepository(connectionString));

        return services;
    }
}
