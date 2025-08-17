using AgentHost.Shared.Contracts;

namespace AgentHost.Worker.Jobs;

public interface IPipelineExecutor
{
    Task ExecuteAsync(Run run, CancellationToken cancellationToken = default);
}

public interface IStepExecutor
{
    bool CanExecute(string stepKind);
    Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken = default);
}

public record StepExecutionContext(
    Guid RunId,
    PipelineStep StepSpec,
    Dictionary<string, object> PipelineContext,
    IServiceProvider ServiceProvider);

public record StepExecutionResult(
    bool Success,
    object? Output = null,
    string? Error = null,
    Dictionary<string, object> Metadata = null!)
{
    public StepExecutionResult() : this(false)
    {
        Metadata ??= new Dictionary<string, object>();
    }
}
