namespace AgentHost.Shared.Contracts;

public record RunRequest(string PipelineName, object PipelineSpec);

public record RunResponse(Guid RunId);

public record RunDetailsResponse(Run Run, IEnumerable<Step> Steps);

public record PipelineStep(
    string Id,
    string Kind,
    string? Tool = null,
    string? Model = null,
    string? System = null,
    string? Input = null,
    string? Fn = null,
    object? Params = null);

public record PipelineSpec(
    string Name,
    IEnumerable<PipelineStep> Steps);

public record HealthCheckResponse(bool Ok, DateTimeOffset Timestamp);

public record ErrorResponse(string Error, string? Details = null);

public record ArtifactCreateRequest(
    string Source,
    string Type,
    string Uri,
    string? Hash = null,
    Dictionary<string, object>? Labels = null);

public record ArtifactResponse(Guid Id);
