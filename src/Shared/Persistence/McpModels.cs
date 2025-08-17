namespace AgentHost.Shared.Persistence;

public record McpServer(
    Guid Id,
    string Name,
    string Type,
    string? DisplayName,
    string? Command,
    string[] Args,
    string? Endpoint,
    Dictionary<string, string> Env,
    bool IsEnabled,
    string Status,
    DateTimeOffset? LastHeartbeatAt,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record McpToolRecord(
    Guid Id,
    Guid ServerId,
    string Name,
    string? Description,
    object Schema,
    string[] RequiredScopes,
    string? Version,
    string? SchemaHash,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
