using System.Text.Json;
using AgentHost.Shared.Clients.McpClient;
using AgentHost.Shared.Contracts;
using Microsoft.Extensions.Logging;

namespace AgentHost.Worker.Jobs;

public class McpStepExecutor : IStepExecutor
{
    private readonly IMcpClient _mcpClient;
    private readonly ILogger<McpStepExecutor> _logger;

    public McpStepExecutor(IMcpClient mcpClient, ILogger<McpStepExecutor> logger)
    {
        _mcpClient = mcpClient;
        _logger = logger;
    }

    public bool CanExecute(string stepKind)
    {
        return stepKind == StepKind.McpPull || stepKind == StepKind.McpCall;
    }

    public async Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.StepSpec.Tool))
        {
            return new StepExecutionResult(false, Error: "Tool is required for MCP steps");
        }

        try
        {
            _logger.LogInformation("Executing MCP step {StepKind} with tool {Tool}", 
                context.StepSpec.Kind, context.StepSpec.Tool);

            // Ensure MCP client is initialized
            await _mcpClient.InitializeAsync(cancellationToken);

            // Convert tool name to MCP format
            var mcpToolName = ConvertToMcpToolName(context.StepSpec.Tool);
            
            var response = await _mcpClient.CallToolAsync(mcpToolName, context.StepSpec.Params, cancellationToken);

            if (response.Success)
            {
                var output = ProcessMcpResponse(context.StepSpec.Kind, response.Result);
                
                _logger.LogInformation("MCP step completed successfully");
                
                return new StepExecutionResult(
                    Success: true,
                    Output: output,
                    Metadata: new Dictionary<string, object>
                    {
                        ["tool"] = mcpToolName,
                        ["originalTool"] = context.StepSpec.Tool,
                        ["mcpMetadata"] = response.Metadata
                    }
                );
            }
            else
            {
                _logger.LogWarning("MCP step failed: {Error}", response.Error);
                return new StepExecutionResult(false, Error: response.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing MCP step");
            return new StepExecutionResult(false, Error: ex.Message);
        }
    }

    private string ConvertToMcpToolName(string originalTool)
    {
        // Convert from legacy format to MCP SDK format
        return originalTool switch
        {
            "mcp.gmail.listMessages" => McpTools.Gmail.ListMessages,
            "mcp.gmail.getMessage" => McpTools.Gmail.GetMessage,
            "mcp.gmail.sendMessage" => McpTools.Gmail.SendMessage,
            "mcp.jira.createIssue" => McpTools.Jira.CreateIssue,
            "mcp.jira.updateIssue" => McpTools.Jira.UpdateIssue,
            "mcp.jira.getIssue" => McpTools.Jira.GetIssue,
            "mcp.fs.readFile" => McpTools.Filesystem.ReadFile,
            "mcp.fs.writeFile" => McpTools.Filesystem.WriteFile,
            "mcp.fs.listFiles" => McpTools.Filesystem.ListFiles,
            _ => originalTool // Return as-is if no mapping found
        };
    }

    private object? ProcessMcpResponse(string stepKind, object? result)
    {
        if (result == null) return null;

        // Different processing based on step kind
        return stepKind switch
        {
            StepKind.McpPull => ProcessPullResponse(result),
            StepKind.McpCall => ProcessCallResponse(result),
            _ => result
        };
    }

    private object ProcessPullResponse(object result)
    {
        // For pull operations, we typically want to extract items
        if (result is JsonElement element && element.TryGetProperty("messages", out var messages))
        {
            return new
            {
                items = messages,
                count = messages.GetArrayLength()
            };
        }

        // Try to serialize and deserialize to handle different object types
        try
        {
            var json = JsonSerializer.Serialize(result);
            var parsedResult = JsonSerializer.Deserialize<JsonElement>(json);
            
            if (parsedResult.TryGetProperty("messages", out var msgs))
            {
                return new
                {
                    items = msgs,
                    count = msgs.GetArrayLength()
                };
            }
        }
        catch
        {
            // If parsing fails, wrap in items structure
        }

        return new { items = result };
    }

    private object ProcessCallResponse(object result)
    {
        // For call operations, we typically want the direct result
        return result;
    }
}
