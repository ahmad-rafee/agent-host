using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace AgentHost.Shared.Observability;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddObservability(this IServiceCollection services, string serviceName, string serviceVersion = "1.0.0")
    {
        var resource = ResourceBuilder.CreateDefault()
            .AddService(serviceName, serviceVersion)
            .AddAttributes(new[]
            {
                new KeyValuePair<string, object>("environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"),
                new KeyValuePair<string, object>("host.name", Environment.MachineName)
            });

        services.AddOpenTelemetry()
            .WithTracing(builder => builder
                .SetResourceBuilder(resource)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddConsoleExporter())
            .WithMetrics(builder => builder
                .SetResourceBuilder(resource)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddConsoleExporter());

        return services;
    }

    public static ILoggingBuilder AddStructuredLogging(this ILoggingBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: 
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} " +
                "{Properties:j}{NewLine}{Exception}")
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service", "AgentHost")
            .CreateLogger();

        builder.ClearProviders();
        builder.AddSerilog(Log.Logger);

        return builder;
    }
}

public static class LoggerExtensions
{
    public static IDisposable BeginRunScope(this Microsoft.Extensions.Logging.ILogger logger, Guid runId, string pipelineName)
    {
        return logger.BeginScope(new Dictionary<string, object>
        {
            ["run_id"] = runId,
            ["pipeline_name"] = pipelineName
        });
    }

    public static IDisposable BeginStepScope(this Microsoft.Extensions.Logging.ILogger logger, Guid stepId, string stepKind)
    {
        return logger.BeginScope(new Dictionary<string, object>
        {
            ["step_id"] = stepId,
            ["step_kind"] = stepKind
        });
    }

    public static void LogRunStarted(this Microsoft.Extensions.Logging.ILogger logger, Guid runId, string pipelineName)
    {
        logger.LogInformation("Run started: {RunId} for pipeline {PipelineName}", runId, pipelineName);
    }

    public static void LogRunCompleted(this Microsoft.Extensions.Logging.ILogger logger, Guid runId, string status, TimeSpan duration)
    {
        logger.LogInformation("Run completed: {RunId} with status {Status} in {Duration}ms", 
            runId, status, duration.TotalMilliseconds);
    }

    public static void LogStepStarted(this Microsoft.Extensions.Logging.ILogger logger, Guid stepId, string kind)
    {
        logger.LogInformation("Step started: {StepId} of kind {Kind}", stepId, kind);
    }

    public static void LogStepCompleted(this Microsoft.Extensions.Logging.ILogger logger, Guid stepId, string status, TimeSpan duration, object? output = null)
    {
        if (output != null)
        {
            logger.LogInformation("Step completed: {StepId} with status {Status} in {Duration}ms, output: {Output}", 
                stepId, status, duration.TotalMilliseconds, output);
        }
        else
        {
            logger.LogInformation("Step completed: {StepId} with status {Status} in {Duration}ms", 
                stepId, status, duration.TotalMilliseconds);
        }
    }

    public static void LogCostAccrued(this Microsoft.Extensions.Logging.ILogger logger, Guid runId, string model, int tokens, decimal cost)
    {
        logger.LogInformation("Cost accrued for run {RunId}: {Cost:C} for {Tokens} tokens using model {Model}", 
            runId, cost, tokens, model);
    }
}
