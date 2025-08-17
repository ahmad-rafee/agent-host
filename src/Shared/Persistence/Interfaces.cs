using AgentHost.Shared.Contracts;

namespace AgentHost.Shared.Persistence;

public interface IRunRepository
{
    Task<Run?> GetAsync(Guid id);
    Task<IEnumerable<Run>> ListAsync(int limit = 100, int offset = 0);
    Task CreateAsync(Run run);
    Task UpdateAsync(Run run);
    Task UpdateStatusAsync(Guid id, string status);
}

public interface IStepRepository
{
    Task<Step?> GetAsync(Guid id);
    Task<IEnumerable<Step>> ListByRunAsync(Guid runId);
    Task CreateAsync(Step step);
    Task UpdateAsync(Step step);
    Task UpdateStatusAsync(Guid id, string status);
}

public interface IArtifactRepository
{
    Task<Artifact?> GetAsync(Guid id);
    Task<IEnumerable<Artifact>> ListAsync(int limit = 100, int offset = 0);
    Task CreateAsync(Artifact artifact);
    Task UpdateAsync(Artifact artifact);
}
