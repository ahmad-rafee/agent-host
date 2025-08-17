using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using System.Text.Json;
using AgentHost.Shared.Persistence;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace AgentHost.Shared.Clients.McpClient;

public class SdkMcpClient : IMcpClient
{
    private readonly ILogger<SdkMcpClient> _logger;
    private readonly McpClientOptions _options;
    // Issue #1: transitioning to real ModelContextProtocol SDK clients per server (lazy). For now we keep a lightweight placeholder runtime object.
    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);
    private IReadOnlyList<McpServer>? _enabledServers; // cached metadata (no processes yet)
    private readonly ConcurrentDictionary<Guid, McpClientContext> _clients = new(); // serverId -> runtime (placeholder until SDK wired)
    private readonly IMcpServerRepository? _serverRepo;
    private readonly IMcpToolRepository? _toolRepo;
    private readonly IServiceScopeFactory? _scopeFactory; // for resolving scoped repos when running as singleton

    public SdkMcpClient(IConfiguration configuration, ILogger<SdkMcpClient> logger)
    {
        _logger = logger;
        _options = configuration.GetSection("Mcp").Get<McpClientOptions>() ?? new McpClientOptions();
    }

    // New constructor for DI with repositories (DB-backed listing)
    public SdkMcpClient(IConfiguration configuration, ILogger<SdkMcpClient> logger, IMcpServerRepository serverRepo, IMcpToolRepository toolRepo)
        : this(configuration, logger)
    {
        _serverRepo = serverRepo;
        _toolRepo = toolRepo;
    }

    // New constructor: capture scope factory instead of scoped repos (avoids resolving scoped from root when singleton)
    public SdkMcpClient(IConfiguration configuration, ILogger<SdkMcpClient> logger, IServiceScopeFactory scopeFactory)
        : this(configuration, logger)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_enabledServers != null) return;
        await _initializationSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_enabledServers != null) return;

            // Load enabled servers from DB (preferred path) else fallback to options config.
            if (_serverRepo != null)
            {
                var servers = await _serverRepo.ListAsync(enabled: true, limit: 500, offset: 0);
                _enabledServers = servers.ToList();
                _logger.LogInformation("[MCP] Cached {ServerCount} enabled servers from DB", _enabledServers.Count);
            }
            else if (_scopeFactory != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IMcpServerRepository>();
                var servers = await repo.ListAsync(enabled: true, limit: 500, offset: 0);
                _enabledServers = servers.ToList();
                _logger.LogInformation("[MCP] Cached {ServerCount} enabled servers (scoped) from DB", _enabledServers.Count);
            }
            else
            {
                // Fallback to static options (legacy mock path)
                _enabledServers = _options.Servers.Select(kvp => new McpServer(
                    Guid.NewGuid(), kvp.Key, kvp.Value.Type, kvp.Key, kvp.Value.Command, kvp.Value.Args, kvp.Value.Type == "http" ? kvp.Value.Command : null,
                    kvp.Value.Env, true, "configured", null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)).ToList();
                _logger.LogInformation("[MCP] Cached {ServerCount} servers from configuration (mock)", _enabledServers.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MCP] Failed to initialize server metadata cache");
            throw;
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    public async Task<McpResponse> CallToolAsync(string toolName, object? parameters = null, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        try
        {
            _logger.LogDebug("Calling MCP tool {ToolName} with parameters {Parameters}", toolName, parameters);
            // Determine server (prefix before first '.') to drive lazy runtime creation.
            var serverKey = toolName.Contains('.') ? toolName.Split('.')[0] : null;
            McpServer? server = null;
            if (serverKey != null && _enabledServers != null)
            {
                server = _enabledServers.FirstOrDefault(s => s.Name.Equals(serverKey, StringComparison.OrdinalIgnoreCase));
            }
            if (server != null)
            {
                var runtime = _clients.GetOrAdd(server.Id, id =>
                {
                    _logger.LogInformation("[MCP] Spawning runtime for server {ServerName} ({ServerId})", server.Name, server.Id);
                    // TODO(issue 001): Replace placeholder with real IMcpClient via McpClientFactory & StdioClientTransport
                    return new McpClientContext();
                });
                // runtime currently unused (placeholder)
            }

            var result = await SimulateToolCallAsync(toolName, parameters, cancellationToken); // still mock until Issue 004

            _logger.LogDebug("MCP tool call completed successfully");
            
            return new McpResponse(
                Success: true,
                Result: result,
                Metadata: new Dictionary<string, object>
                {
                    ["toolName"] = toolName,
                    ["simulated"] = true,
                    ["timestamp"] = DateTimeOffset.UtcNow
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling MCP tool {ToolName}", toolName);
            return new McpResponse(false, Error: ex.Message);
        }
    }

    public async Task<IEnumerable<McpTool>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        try
        {
            _logger.LogDebug("Listing available MCP tools");

            // If repositories were injected directly OR we have a scope factory, resolve via whichever path is available
            async Task<IEnumerable<McpTool>> LoadFromDbAsync(IMcpServerRepository serverRepo, IMcpToolRepository toolRepo)
            {
                var servers = await serverRepo.ListAsync(enabled: true, limit: 500, offset: 0);
                var result = new List<McpTool>();
                foreach (var server in servers)
                {
                    var tools = await toolRepo.ListByServerAsync(server.Id, includeDeleted: false, limit: 500, offset: 0);
                    foreach (var t in tools.Where(t => !t.IsDeleted))
                    {
                        var json = JsonSerializer.Serialize(t.Schema);
                        using var doc = JsonDocument.Parse(json);
                        result.Add(new McpTool(
                            t.Name,
                            t.Description ?? string.Empty,
                            doc.RootElement.Clone(),
                            t.RequiredScopes));
                    }
                }
                _logger.LogInformation("Found {ToolCount} MCP tools (DB)", result.Count);
                return result;
            }

            if (_serverRepo != null && _toolRepo != null)
            {
                return await LoadFromDbAsync(_serverRepo, _toolRepo);
            }
            if (_scopeFactory != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var sr = scope.ServiceProvider.GetRequiredService<IMcpServerRepository>();
                var tr = scope.ServiceProvider.GetRequiredService<IMcpToolRepository>();
                return await LoadFromDbAsync(sr, tr);
            }

            // Fallback to mock tools (legacy path/tests)
            var mockTools = GetMockTools();
            _logger.LogInformation("Found {ToolCount} MCP tools (mock)", mockTools.Count());
            return mockTools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing MCP tools");
            return Array.Empty<McpTool>();
        }
    }

    // NOTE: Previous single mock client removed; we now manage per-server runtimes lazily (placeholders until SDK integration).

    private async Task<object> SimulateToolCallAsync(string toolName, object? parameters, CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken); // Simulate network call

        var normalized = Normalize(toolName);
        return normalized switch
        {
            "gmail.list_messages" => SimulateGmailListMessages(parameters),
            "jira.create_issue" => SimulateJiraCreateIssue(parameters),
            "fs.read_file" => SimulateFileSystemRead(parameters),
            _ => throw new ArgumentException($"Unknown tool: {toolName}")
        };
    }

    private static string Normalize(string name)
    {
        if (name.StartsWith("mcp."))
        {
            var parts = name.Split('.', 3);
            if (parts.Length == 3)
            {
                var server = parts[1];
                var action = parts[2];
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < action.Length; i++)
                {
                    var c = action[i];
                    if (i > 0 && char.IsUpper(c)) sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                return $"{server}.{sb}";
            }
        }
        return name;
    }

    private object SimulateGmailListMessages(object? parameters)
    {
        return new
        {
            messages = new[]
            {
                new { 
                    id = "msg_001", 
                    subject = "Action Required: Review Q4 Budget", 
                    from = "manager@company.com", 
                    date = DateTimeOffset.UtcNow.AddHours(-2),
                    snippet = "Please review the Q4 budget proposal and provide feedback by EOD."
                },
                new { 
                    id = "msg_002", 
                    subject = "Follow up on client presentation", 
                    from = "client@example.com", 
                    date = DateTimeOffset.UtcNow.AddHours(-1),
                    snippet = "Thank you for the presentation. We have a few questions about the timeline."
                }
            },
            totalCount = 2,
            query = parameters?.ToString() ?? "recent emails"
        };
    }

    private object SimulateJiraCreateIssue(object? parameters)
    {
        var issueKey = "DEMO-" + Random.Shared.Next(1000, 9999);
        return new
        {
            key = issueKey,
            id = Guid.NewGuid().ToString(),
            url = $"https://company.atlassian.net/browse/{issueKey}",
            status = "Created",
            created = DateTimeOffset.UtcNow
        };
    }

    private object SimulateFileSystemRead(object? parameters)
    {
        return new
        {
            content = "Sample file content for demonstration purposes.",
            path = parameters?.ToString() ?? "/sample/path",
            size = 42,
            lastModified = DateTimeOffset.UtcNow.AddDays(-1)
        };
    }

    private static IEnumerable<McpTool> GetMockTools()
    {
        return new[]
        {
            new McpTool(
                "gmail.list_messages",
                "List Gmail messages based on query parameters",
                JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "query": {
                            "type": "string",
                            "description": "Gmail search query (e.g., 'label:inbox newer_than:1d')"
                        },
                        "maxResults": {
                            "type": "integer",
                            "description": "Maximum number of messages to return",
                            "default": 10
                        }
                    }
                }
                """).RootElement,
                new[] { "email.read" }
            ),
            new McpTool(
                "jira.create_issue",
                "Create a new Jira issue",
                JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "project": {
                            "type": "string",
                            "description": "Jira project key"
                        },
                        "summary": {
                            "type": "string",
                            "description": "Issue summary/title"
                        },
                        "description": {
                            "type": "string",
                            "description": "Issue description"
                        },
                        "issueType": {
                            "type": "string",
                            "description": "Issue type (e.g., 'Task', 'Bug', 'Story')",
                            "default": "Task"
                        }
                    },
                    "required": ["project", "summary"]
                }
                """).RootElement,
                new[] { "jira.write" }
            ),
            new McpTool(
                "fs.read_file",
                "Read contents of a file",
                JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "path": {
                            "type": "string",
                            "description": "File path to read"
                        }
                    },
                    "required": ["path"]
                }
                """).RootElement,
                new[] { "filesystem.read" }
            )
        };
    }

    public void Dispose()
    {
        foreach (var kvp in _clients)
        {
            try
            {
                kvp.Value.Dispose();
                _logger.LogInformation("[MCP] Disposed runtime for server {ServerId}", kvp.Key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[MCP] Error disposing runtime for server {ServerId}", kvp.Key);
            }
        }
        _initializationSemaphore.Dispose();
    }
}

// Placeholder for the actual MCP client context from the SDK
public class McpClientContext : IDisposable
{
    public void Dispose()
    {
        // Cleanup resources
    }
}

public class McpClientOptions
{
    public Dictionary<string, McpServerConfig> Servers { get; set; } = new()
    {
        ["gmail"] = new McpServerConfig { Command = "mock-gmail-server", Type = "stdio" },
        ["jira"] = new McpServerConfig { Command = "mock-jira-server", Type = "stdio" },
        ["filesystem"] = new McpServerConfig { Command = "mock-fs-server", Type = "stdio" }
    };
    
    public bool UseMockResponses { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 30;
}

public class McpServerConfig
{
    public string Command { get; set; } = "";
    public string[] Args { get; set; } = Array.Empty<string>();
    public string Type { get; set; } = "stdio";
    public Dictionary<string, string> Env { get; set; } = new();
}
