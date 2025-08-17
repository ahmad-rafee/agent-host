using System.Text.Json;
using AgentHost.Shared.Contracts;
using Microsoft.Extensions.Logging;

namespace AgentHost.Worker.Jobs;

public class TransformStepExecutor : IStepExecutor
{
    private readonly ILogger<TransformStepExecutor> _logger;

    public TransformStepExecutor(ILogger<TransformStepExecutor> logger)
    {
        _logger = logger;
    }

    public bool CanExecute(string stepKind)
    {
        return stepKind == StepKind.Transform;
    }

    public async Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var function = context.StepSpec.Fn;
            var input = ResolveInput(context.StepSpec.Input, context.PipelineContext);

            _logger.LogInformation("Executing transform step with function {Function}", function);

            var output = function switch
            {
                "emailToDocV1" => TransformEmailToDoc(input),
                "normalizeText" => NormalizeText(input),
                "extractJson" => ExtractJsonFromText(input),
                _ => PassThrough(input)
            };

            await Task.CompletedTask; // Make async

            _logger.LogInformation("Transform step completed");

            return new StepExecutionResult(
                Success: true,
                Output: output,
                Metadata: new Dictionary<string, object>
                {
                    ["function"] = function ?? "passthrough",
                    ["inputType"] = input?.GetType().Name ?? "null",
                    ["outputType"] = output?.GetType().Name ?? "null"
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing transform step");
            return new StepExecutionResult(false, Error: ex.Message);
        }
    }

    private object? ResolveInput(string? inputReference, Dictionary<string, object> context)
    {
        if (string.IsNullOrEmpty(inputReference))
            return null;

        if (inputReference.StartsWith("@"))
        {
            var reference = inputReference[1..];
            var parts = reference.Split('.');
            
            if (parts.Length > 0 && context.TryGetValue(parts[0], out var stepOutput))
            {
                if (parts.Length > 1 && stepOutput != null)
                {
                    return ExtractNestedProperty(stepOutput, parts.Skip(1).ToArray());
                }
                
                return stepOutput;
            }

            _logger.LogWarning("Could not resolve input reference: {Input}", inputReference);
            return null;
        }

        return inputReference;
    }

    private object? ExtractNestedProperty(object obj, string[] propertyPath)
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
                    return null;
                }
            }

            return JsonSerializer.Deserialize<object>(element.GetRawText());
        }
        catch
        {
            return obj;
        }
    }

    private object TransformEmailToDoc(object? input)
    {
        if (input == null) return new { docs = Array.Empty<object>() };

        try
        {
            var json = JsonSerializer.Serialize(input);
            var element = JsonSerializer.Deserialize<JsonElement>(json);

            if (element.TryGetProperty("items", out var items))
            {
                var docs = new List<object>();
                
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("subject", out var subject) &&
                        item.TryGetProperty("from", out var from))
                    {
                        docs.Add(new
                        {
                            id = item.TryGetProperty("id", out var id) ? id.GetString() : Guid.NewGuid().ToString(),
                            title = subject.GetString(),
                            content = $"Email from {from.GetString()}: {subject.GetString()}",
                            type = "email",
                            metadata = new
                            {
                                from = from.GetString(),
                                date = item.TryGetProperty("date", out var date) ? date.GetString() : DateTimeOffset.UtcNow.ToString()
                            }
                        });
                    }
                }

                return new { docs };
            }

            return new { docs = new[] { new { id = Guid.NewGuid().ToString(), content = json, type = "unknown" } } };
        }
        catch
        {
            return new { docs = Array.Empty<object>() };
        }
    }

    private object NormalizeText(object? input)
    {
        if (input == null) return "";

        var text = input.ToString() ?? "";
        
        // Simple text normalization
        return new
        {
            normalized = text.Trim().ToLowerInvariant(),
            original = text,
            length = text.Length
        };
    }

    private object? ExtractJsonFromText(object? input)
    {
        if (input == null) return null;

        var text = input.ToString() ?? "";
        
        // Try to find JSON in the text
        var jsonStart = text.IndexOf('{');
        var jsonEnd = text.LastIndexOf('}');
        
        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            var jsonText = text[jsonStart..(jsonEnd + 1)];
            try
            {
                return JsonSerializer.Deserialize<object>(jsonText);
            }
            catch
            {
                return new { error = "Failed to parse JSON", text = jsonText };
            }
        }

        return new { error = "No JSON found in text", originalText = text };
    }

    private object PassThrough(object? input)
    {
        return input ?? new { };
    }
}
