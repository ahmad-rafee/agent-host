using System.Text.Json;
using ModelContextProtocol.Client;

namespace AgentHost.Shared.Clients.McpClient;

public interface IMcpClient
{
    Task<McpResponse> CallToolAsync(string toolName, object? parameters = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<McpTool>> ListToolsAsync(CancellationToken cancellationToken = default);
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public record McpResponse(
    bool Success,
    object? Result = null,
    string? Error = null,
    Dictionary<string, object> Metadata = null!)
{
    public McpResponse() : this(false)
    {
        Metadata ??= new Dictionary<string, object>();
    }
}

public record McpTool(
    string Name,
    string Description,
    JsonElement Schema,
    IEnumerable<string> RequiredScopes = null!)
{
    public McpTool() : this("", "", new JsonElement())
    {
        RequiredScopes ??= Array.Empty<string>();
    }
}
