using System.Text.Json;
using AgentHost.Shared.Clients.LlmClient;
using AgentHost.Shared.Contracts;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace AgentHost.Worker.Jobs;

public class LlmStepExecutor : IStepExecutor
{
    private readonly ILlmClient _llmClient;
    private readonly ILogger<LlmStepExecutor> _logger;

    public LlmStepExecutor(ILlmClient llmClient, ILogger<LlmStepExecutor> logger)
    {
        _llmClient = llmClient;
        _logger = logger;
    }

    public bool CanExecute(string stepKind)
    {
        return stepKind == StepKind.LlmPrompt;
    }

    public async Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var model = context.StepSpec.Model ?? "chat-default";
            var systemPrompt = context.StepSpec.System;
            var input = ResolveInput(context.StepSpec.Input, context.PipelineContext);

            _logger.LogInformation("Executing LLM step with model {Model}", model);

            var messages = new List<LlmMessage>();
            
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new LlmMessage(LlmRoles.System, systemPrompt));
            }

            messages.Add(new LlmMessage(LlmRoles.User, input ?? "Please analyze the provided data."));

            var request = new LlmChatRequest(
                model,
                messages,
                Temperature: 0.1 // Lower temperature for more consistent results
            );

            var response = await _llmClient.ChatAsync(request, cancellationToken);

            // Try to parse as JSON if it looks like JSON
            object output = response.Content;
            if (response.Content.Trim().StartsWith("{") || response.Content.Trim().StartsWith("["))
            {
                try
                {
                    output = JsonSerializer.Deserialize<object>(response.Content);
                }
                catch
                {
                    // Keep as string if JSON parsing fails
                }
            }

            _logger.LogInformation("LLM step completed. Tokens used: {TotalTokens}", response.Usage.TotalTokens);

            return new StepExecutionResult(
                Success: true,
                Output: output,
                Metadata: new Dictionary<string, object>
                {
                    ["model"] = response.Model,
                    ["usage"] = response.Usage,
                    ["inputLength"] = input?.Length ?? 0,
                    ["outputLength"] = response.Content.Length
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing LLM step");
            return new StepExecutionResult(false, Error: ex.Message);
        }
    }

    private string? ResolveInput(string? input, Dictionary<string, object> context)
    {
        if (string.IsNullOrEmpty(input))
            return null;

        // Handle simple variable references like "@ingest.items"
        if (input.StartsWith("@"))
        {
            var reference = input[1..]; // Remove @ prefix
            var parts = reference.Split('.');
            
            if (parts.Length > 0 && context.TryGetValue(parts[0], out var stepOutput))
            {
                // If we have nested property access
                if (parts.Length > 1)
                {
                    return ExtractNestedProperty(stepOutput, parts.Skip(1).ToArray());
                }
                
                return JsonSerializer.Serialize(stepOutput);
            }

            _logger.LogWarning("Could not resolve input reference: {Input}", input);
            return input;
        }

        return input;
    }

    private string ExtractNestedProperty(object obj, string[] propertyPath)
    {
        try
        {
            var json = JsonSerializer.Serialize(obj);
            var element = JsonSerializer.Deserialize<JsonElement>(json);

            foreach (var property in propertyPath)
            {
                if (element.TryGetProperty(property, out var nestedElement))
                {
                    element = nestedElement;
                }
                else
                {
                    _logger.LogWarning("Property {Property} not found in object", property);
                    return JsonSerializer.Serialize(obj);
                }
            }

            return element.GetRawText();
        }
        catch
        {
            return JsonSerializer.Serialize(obj);
        }
    }
}
