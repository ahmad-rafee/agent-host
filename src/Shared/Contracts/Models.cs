namespace AgentHost.Shared.Contracts;

public record Run(
    Guid Id,
    string PipelineName,
    object PipelineSpec,
    string Status,
    Dictionary<string, object> Costs,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public Run() : this(Guid.Empty, "", new { }, "", new Dictionary<string, object>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow) {}
}

public record Step(
    Guid Id,
    Guid RunId,
    string Kind,
    string Status,
    object? Input,
    object? Output,
    int Attempt,
    Dictionary<string, object> Timings,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public Step() : this(Guid.Empty, Guid.Empty, "", "", null, null, 0, new Dictionary<string, object>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow) {}
}

public record Artifact(
    Guid Id,
    string Source,
    string Type,
    string Uri,
    string? Hash,
    Dictionary<string, object> Labels,
    DateTimeOffset CreatedAt)
{
    public Artifact() : this(Guid.Empty, "", "", "", null, new Dictionary<string, object>(), DateTimeOffset.UtcNow) {}
}

public static class RunStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

public static class StepStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Skipped = "skipped";
}

public static class StepKind
{
    public const string McpPull = "mcp.pull";
    public const string Transform = "transform";
    public const string LlmPrompt = "llm.prompt";
    public const string McpCall = "mcp.call";
    public const string VectorEmbed = "vector.embed";
    public const string Rag = "rag";
}

public static class ArtifactType
{
    public const string Email = "email";
    public const string Note = "note";
    public const string Document = "doc";
}
