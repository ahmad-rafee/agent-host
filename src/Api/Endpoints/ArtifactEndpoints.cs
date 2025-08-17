using AgentHost.Shared.Contracts;
using AgentHost.Shared.Persistence;
using Microsoft.AspNetCore.Mvc;
using AgentHost.Api.Infrastructure;

namespace AgentHost.Api.Endpoints;

public static class ArtifactEndpoints
{
    public static void MapArtifactEndpoints(this WebApplication app)
    {
        app.MapPost("/artifacts", async (
            [FromBody] ArtifactCreateRequest request,
            [FromServices] IArtifactRepository artifactRepository,
            [FromServices] ILogger<Program> logger) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Source) || string.IsNullOrWhiteSpace(request.Type) || string.IsNullOrWhiteSpace(request.Uri))
                {
                    return BufferedResults.BadRequest(new ErrorResponse("source, type and uri are required"));
                }

                var artifact = new Artifact(
                    Id: Guid.NewGuid(),
                    Source: request.Source,
                    Type: request.Type,
                    Uri: request.Uri,
                    Hash: request.Hash,
                    Labels: request.Labels ?? new Dictionary<string, object>(),
                    CreatedAt: DateTimeOffset.UtcNow);

                await artifactRepository.CreateAsync(artifact);
                logger.LogInformation("Created artifact {ArtifactId} of type {Type}", artifact.Id, artifact.Type);
                return BufferedResults.Created($"/artifacts/{artifact.Id}", new ArtifactResponse(artifact.Id));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating artifact");
                return BufferedResults.Problem("An error occurred while creating the artifact");
            }
        })
        .WithName("CreateArtifact")
        .WithSummary("Create a new artifact")
        .WithTags("Artifacts");

        app.MapGet("/artifacts/{id:guid}", async (
            Guid id,
            [FromServices] IArtifactRepository artifactRepository,
            [FromServices] ILogger<Program> logger) =>
        {
            try
            {
                var artifact = await artifactRepository.GetAsync(id);
                if (artifact == null)
                {
                    return BufferedResults.NotFound(new ErrorResponse($"Artifact {id} not found"));
                }

                return BufferedResults.Ok(artifact);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving artifact {ArtifactId}", id);
                return BufferedResults.Problem("An error occurred while retrieving the artifact");
            }
        })
        .WithName("GetArtifact")
        .WithSummary("Get artifact metadata")
        .WithTags("Artifacts");

        app.MapGet("/artifacts", async (
            [FromServices] IArtifactRepository artifactRepository,
            [FromServices] ILogger<Program> logger,
            [FromQuery] int limit = 100,
            [FromQuery] int offset = 0) =>
        {
            try
            {
                var artifacts = await artifactRepository.ListAsync(limit, offset);
                return BufferedResults.Ok(artifacts);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving artifacts");
                return BufferedResults.Problem("An error occurred while retrieving artifacts");
            }
        })
        .WithName("ListArtifacts")
        .WithSummary("List artifacts")
        .WithTags("Artifacts");
    }
}
