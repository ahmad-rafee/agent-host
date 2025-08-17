using System.Diagnostics;
using System.Text.Json;
using AgentHost.Shared.Contracts;
using AgentHost.Shared.Observability;
using AgentHost.Shared.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentHost.Worker.Jobs;

public class PipelineExecutor : IPipelineExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IRunRepository _runRepository;
    private readonly IStepRepository _stepRepository;
    private readonly IEnumerable<IStepExecutor> _stepExecutors;
    private readonly ILogger<PipelineExecutor> _logger;

    public PipelineExecutor(
        IServiceProvider serviceProvider,
        IRunRepository runRepository,
        IStepRepository stepRepository,
        IEnumerable<IStepExecutor> stepExecutors,
        ILogger<PipelineExecutor> logger)
    {
        _serviceProvider = serviceProvider;
        _runRepository = runRepository;
        _stepRepository = stepRepository;
        _stepExecutors = stepExecutors;
        _logger = logger;
    }

    public async Task ExecuteAsync(Run run, CancellationToken cancellationToken = default)
    {
        using var runScope = _logger.BeginRunScope(run.Id, run.PipelineName);
        
        var stopwatch = Stopwatch.StartNew();
        _logger.LogRunStarted(run.Id, run.PipelineName);

        try
        {
            // Mark run as running
            await _runRepository.UpdateStatusAsync(run.Id, RunStatus.Running);

            // Parse pipeline spec
            var pipelineSpec = JsonSerializer.Deserialize<PipelineSpec>(JsonSerializer.Serialize(run.PipelineSpec));
            if (pipelineSpec == null)
            {
                throw new InvalidOperationException("Invalid pipeline specification");
            }

            // Execute steps sequentially
            var pipelineContext = new Dictionary<string, object>();
            var allStepsSucceeded = true;

            foreach (var stepSpec in pipelineSpec.Steps)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Pipeline execution cancelled");
                    await _runRepository.UpdateStatusAsync(run.Id, RunStatus.Failed);
                    return;
                }

                var stepResult = await ExecuteStepAsync(run.Id, stepSpec, pipelineContext, cancellationToken);
                
                if (stepResult.Success && stepResult.Output != null)
                {
                    pipelineContext[stepSpec.Id] = stepResult.Output;
                }
                else
                {
                    allStepsSucceeded = false;
                    _logger.LogError("Step {StepId} failed: {Error}", stepSpec.Id, stepResult.Error);
                    
                    // For now, fail the entire pipeline if any step fails
                    // In production, you might want to implement retry logic or continue with other steps
                    break;
                }
            }

            // Update final run status
            var finalStatus = allStepsSucceeded ? RunStatus.Completed : RunStatus.Failed;
            await _runRepository.UpdateStatusAsync(run.Id, finalStatus);

            stopwatch.Stop();
            _logger.LogRunCompleted(run.Id, finalStatus, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Pipeline execution failed for run {RunId}", run.Id);
            
            await _runRepository.UpdateStatusAsync(run.Id, RunStatus.Failed);
            _logger.LogRunCompleted(run.Id, RunStatus.Failed, stopwatch.Elapsed);
        }
    }

    private async Task<StepExecutionResult> ExecuteStepAsync(
        Guid runId, 
        PipelineStep stepSpec, 
        Dictionary<string, object> pipelineContext,
        CancellationToken cancellationToken)
    {
        var stepId = Guid.NewGuid();
        using var stepScope = _logger.BeginStepScope(stepId, stepSpec.Kind);

        var stopwatch = Stopwatch.StartNew();
        _logger.LogStepStarted(stepId, stepSpec.Kind);

        // Create step record
        var step = new Step(
            Id: stepId,
            RunId: runId,
            Kind: stepSpec.Kind,
            Status: StepStatus.Running,
            Input: stepSpec.Params ?? stepSpec.Input,
            Output: null,
            Attempt: 0,
            Timings: new Dictionary<string, object>(),
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);

        await _stepRepository.CreateAsync(step);

        try
        {
            // Find appropriate executor
            var executor = _stepExecutors.FirstOrDefault(e => e.CanExecute(stepSpec.Kind));
            if (executor == null)
            {
                var error = $"No executor found for step kind: {stepSpec.Kind}";
                await UpdateStepStatus(stepId, StepStatus.Failed, error: error);
                return new StepExecutionResult(false, Error: error);
            }

            // Execute step
            var context = new StepExecutionContext(runId, stepSpec, pipelineContext, _serviceProvider);
            var result = await executor.ExecuteAsync(context, cancellationToken);

            // Update step record
            var finalStatus = result.Success ? StepStatus.Completed : StepStatus.Failed;
            await UpdateStepStatus(stepId, finalStatus, result.Output, result.Error, result.Metadata);

            stopwatch.Stop();
            _logger.LogStepCompleted(stepId, finalStatus, stopwatch.Elapsed, result.Output);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Step execution failed");
            
            await UpdateStepStatus(stepId, StepStatus.Failed, error: ex.Message);
            _logger.LogStepCompleted(stepId, StepStatus.Failed, stopwatch.Elapsed);

            return new StepExecutionResult(false, Error: ex.Message);
        }
    }

    private async Task UpdateStepStatus(Guid stepId, string status, object? output = null, string? error = null, Dictionary<string, object>? metadata = null)
    {
        var step = await _stepRepository.GetAsync(stepId);
        if (step != null)
        {
            var updatedStep = step with
            {
                Status = status,
                Output = output,
                Timings = metadata ?? new Dictionary<string, object>(),
                UpdatedAt = DateTimeOffset.UtcNow
            };

            if (!string.IsNullOrEmpty(error))
            {
                updatedStep = updatedStep with
                {
                    Output = new { error, originalOutput = output }
                };
            }

            await _stepRepository.UpdateAsync(updatedStep);
        }
    }
}
