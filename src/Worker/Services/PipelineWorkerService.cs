using AgentHost.Shared.Contracts;
using AgentHost.Shared.Observability;
using AgentHost.Shared.Persistence;
using AgentHost.Worker.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentHost.Worker.Services;

public class PipelineWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PipelineWorkerService> _logger;

    public PipelineWorkerService(IServiceProvider serviceProvider, ILogger<PipelineWorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Pipeline Worker Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingRunsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pending runs");
            }

            // Wait before checking for more work
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        _logger.LogInformation("Pipeline Worker Service stopped");
    }

    private async Task ProcessPendingRunsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var runRepository = scope.ServiceProvider.GetRequiredService<IRunRepository>();
        var pipelineExecutor = scope.ServiceProvider.GetRequiredService<IPipelineExecutor>();

        // Get pending runs (in production, this would use a proper queue/message broker)
        var pendingRuns = await GetPendingRunsAsync(runRepository);

        foreach (var run in pendingRuns)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                _logger.LogInformation("Processing run {RunId} for pipeline {PipelineName}", 
                    run.Id, run.PipelineName);

                // Execute pipeline in background task to allow parallel processing
                _ = Task.Run(async () =>
                {
                    using var executionScope = _serviceProvider.CreateScope();
                    var executor = executionScope.ServiceProvider.GetRequiredService<IPipelineExecutor>();
                    
                    try
                    {
                        await executor.ExecuteAsync(run, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing pipeline for run {RunId}", run.Id);
                    }
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting pipeline execution for run {RunId}", run.Id);
            }
        }
    }

    private async Task<IEnumerable<Run>> GetPendingRunsAsync(IRunRepository runRepository)
    {
        try
        {
            // Simple polling approach for PoC
            // In production, use a message queue or database triggers
            var allRuns = await runRepository.ListAsync(limit: 10);
            return allRuns.Where(r => r.Status == RunStatus.Pending);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending runs");
            return Array.Empty<Run>();
        }
    }
}
