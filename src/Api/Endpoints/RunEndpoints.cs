using AgentHost.Api.Pipelines;
using AgentHost.Shared.Contracts;
using AgentHost.Shared.Persistence;
using AgentHost.Shared.Policy;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using AgentHost.Api.Infrastructure;

namespace AgentHost.Api.Endpoints;

public static class RunEndpoints
{
    public static void MapRunEndpoints(this WebApplication app)
    {
        app.MapPost("/runs", async (
            [FromBody] RunRequest request,
            [FromServices] IRunRepository runRepository,
            [FromServices] IPipelineRegistry pipelineRegistry,
            [FromServices] IPolicyValidator policyValidator,
            [FromServices] ILogger<Program> logger) =>
        {
            try
            {
                // Validate pipeline exists
                var pipelineSpec = pipelineRegistry.GetPipeline(request.PipelineName);
                if (pipelineSpec == null)
                {
                    return BufferedResults.BadRequest(new ErrorResponse($"Pipeline '{request.PipelineName}' not found"));
                }

                // Validate policy
                var policyRequest = new PolicyValidationRequest(
                    UserId: "poc-user", // In production, get from auth context
                    Model: ExtractModelFromPipeline(pipelineSpec),
                    Tools: ExtractToolsFromPipeline(pipelineSpec)
                );

                var policyResult = await policyValidator.ValidateAsync(policyRequest);
                if (!policyResult.IsValid)
                {
                    return BufferedResults.BadRequest(new ErrorResponse("Policy validation failed", 
                        string.Join(", ", policyResult.Errors)));
                }

                // Create run
                var runId = Guid.NewGuid();
                var run = new Run(
                    Id: runId,
                    PipelineName: request.PipelineName,
                    PipelineSpec: pipelineSpec,
                    Status: RunStatus.Pending,
                    Costs: new Dictionary<string, object>(),
                    CreatedAt: DateTimeOffset.UtcNow,
                    UpdatedAt: DateTimeOffset.UtcNow);

                await runRepository.CreateAsync(run);

                logger.LogInformation("Created run {RunId} for pipeline {PipelineName}", runId, request.PipelineName);

                return BufferedResults.Created($"/runs/{runId}", new RunResponse(runId));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating run for pipeline {PipelineName}", request.PipelineName);
                return BufferedResults.Problem("An error occurred while creating the run");
            }
        })
        .WithName("CreateRun")
        .WithSummary("Create a new pipeline run")
        .WithTags("Runs");

        app.MapGet("/runs/{id:guid}", async (
            Guid id,
            [FromServices] IRunRepository runRepository,
            [FromServices] IStepRepository stepRepository,
            [FromServices] ILogger<Program> logger) =>
        {
            try
            {
                var run = await runRepository.GetAsync(id);
                if (run == null)
                {
                    return BufferedResults.NotFound(new ErrorResponse($"Run {id} not found"));
                }

                var steps = await stepRepository.ListByRunAsync(id);

                return BufferedResults.Ok(new RunDetailsResponse(run, steps));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving run {RunId}", id);
                return BufferedResults.Problem("An error occurred while retrieving the run");
            }
        })
        .WithName("GetRun")
        .WithSummary("Get run details")
        .WithTags("Runs");

        app.MapGet("/runs", async (
            [FromServices] IRunRepository runRepository,
            [FromServices] ILogger<Program> logger,
            [FromQuery] int limit = 100,
            [FromQuery] int offset = 0) =>
        {
            try
            {
                var runs = await runRepository.ListAsync(limit, offset);
                return BufferedResults.Ok(runs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving runs");
                return BufferedResults.Problem("An error occurred while retrieving runs");
            }
        })
        .WithName("ListRuns")
        .WithSummary("List pipeline runs")
        .WithTags("Runs");
    }

    private static string ExtractModelFromPipeline(PipelineSpec pipelineSpec)
    {
        return pipelineSpec.Steps
            .Where(s => s.Kind == StepKind.LlmPrompt)
            .Select(s => s.Model)
            .FirstOrDefault(m => !string.IsNullOrEmpty(m)) ?? "chat-default";
    }

    private static IEnumerable<string> ExtractToolsFromPipeline(PipelineSpec pipelineSpec)
    {
        return pipelineSpec.Steps
            .Where(s => s.Kind == StepKind.McpPull || s.Kind == StepKind.McpCall)
            .Select(s => s.Tool)
            .Where(t => !string.IsNullOrEmpty(t))
            .Cast<string>();
    }
}
