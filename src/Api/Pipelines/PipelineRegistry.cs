using System.Text.Json;
using AgentHost.Shared.Contracts;

namespace AgentHost.Api.Pipelines;

public interface IPipelineRegistry
{
    PipelineSpec? GetPipeline(string name);
    IEnumerable<string> GetPipelineNames();
    void RegisterPipeline(PipelineSpec pipeline);
}

public class InMemoryPipelineRegistry : IPipelineRegistry
{
    private readonly Dictionary<string, PipelineSpec> _pipelines = new();

    public InMemoryPipelineRegistry()
    {
        // Register default PoC pipeline
        RegisterDefaultPipelines();
    }

    public PipelineSpec? GetPipeline(string name)
    {
        return _pipelines.GetValueOrDefault(name);
    }

    public IEnumerable<string> GetPipelineNames()
    {
        return _pipelines.Keys;
    }

    public void RegisterPipeline(PipelineSpec pipeline)
    {
        _pipelines[pipeline.Name] = pipeline;
    }

    private void RegisterDefaultPipelines()
    {
        // Register the example pipeline from the project plan
        var actionItemsPipeline = new PipelineSpec(
            "action-items-from-emails",
            new[]
            {
                new PipelineStep(
                    "ingest",
                    StepKind.McpPull,
                    Tool: "mcp.gmail.listMessages",
                    Params: new { query = "label:inbox newer_than:10m" }),

                new PipelineStep(
                    "normalize",
                    StepKind.Transform,
                    Fn: "emailToDocV1",
                    Input: "@ingest.items"),

                new PipelineStep(
                    "analyze",
                    StepKind.LlmPrompt,
                    Model: "chat-default",
                    System: "Extract actionable items as JSON.",
                    Input: "@normalize.docs"),

                new PipelineStep(
                    "act",
                    StepKind.McpCall,
                    Tool: "mcp.jira.createIssue",
                    Params: new { project = "OPS", payload = "@analyze.json" })
            });

        RegisterPipeline(actionItemsPipeline);

        // Register a simple test pipeline
        var testPipeline = new PipelineSpec(
            "test-pipeline",
            new[]
            {
                new PipelineStep(
                    "pull-data",
                    StepKind.McpPull,
                    Tool: "mcp.gmail.listMessages",
                    Params: new { query = "test" }),

                new PipelineStep(
                    "analyze-data",
                    StepKind.LlmPrompt,
                    Model: "chat-default",
                    System: "Analyze the provided data and provide insights.",
                    Input: "@pull-data.result")
            });

        RegisterPipeline(testPipeline);
    }
}
